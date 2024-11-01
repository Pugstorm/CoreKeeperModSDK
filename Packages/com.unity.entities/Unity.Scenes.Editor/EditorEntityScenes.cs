using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core.Compression;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Entities.Baking;
using Unity.Entities.Serialization;
using Unity.Entities.Streaming;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hash128 = Unity.Entities.Hash128;
using UnityObject = UnityEngine.Object;
using AssetImportContext = UnityEditor.AssetImporters.AssetImportContext;

namespace Unity.Scenes.Editor
{
    internal struct WriteEntitySceneSettings
    {
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
        public string OutputPath;
        public Codec Codec;
        public Entity PrefabRoot;
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
    }

    /// <summary>
    /// Provides utility methods for dealing with entity sub scenes in the editor.
    /// </summary>
    public static class EditorEntityScenes
    {
        static readonly ProfilerMarker k_ProfileEntitiesSceneSave = new ProfilerMarker("EntitiesScene.Save");
        static readonly ProfilerMarker k_ProfileEntitiesSceneSaveHeader = new ProfilerMarker("EntitiesScene.WriteHeader");
        static readonly ProfilerMarker k_ProfileEntitiesSceneWriteObjRefs = new ProfilerMarker("EntitiesScene.WriteObjectReferences");

#region Baking
        static void RegisterDependencies(AssetImportContext importContext, UnsafeList<BakeDependencies.AssetState> assetDependencies)
        {
            for (int i = 0; i < assetDependencies.Length; i++)
            {
                var guid = assetDependencies[i].GUID;
                if (GUIDHelper.IsBuiltin(in guid))
                {
                    // AssetImportContext does not support dependencies on inbuilt assets
                    continue;
                }
                if (guid.Empty())
                    continue;
                importContext.DependsOnArtifact(guid);
            }
        }

        internal static SceneSectionData[] BakeAndWriteEntityScene(Scene scene, BakingSettings settings, List<ReferencedUnityObjects> sectionRefObjs, WriteEntitySceneSettings writeEntitySettings)
        {
            using var world = new World("EditorScenesBakingWorld");

            bool disposeBlobAssetCache = false;
            if (!settings.BlobAssetStore.IsCreated)
            {
                settings.BlobAssetStore = new BlobAssetStore(128);
                disposeBlobAssetCache = true;
            }

            BakingUtility.BakeScene(world, scene, settings, false, null);

            var bakingSystem = world.GetExistingSystemManaged<BakingSystem>();

            if (settings.PrefabRoot != null)
                writeEntitySettings.PrefabRoot = bakingSystem.GetEntity(settings.PrefabRoot);

            // The importer needs to depend on all asset artifacts
            if (settings.AssetImportContext != null)
                RegisterDependencies(settings.AssetImportContext, bakingSystem.GetAllAssetDependencies());

            // Optimizing and writing the scene is done here to include potential log messages in the conversion log.
            EntitySceneOptimization.Optimize(world);

            var sections = WriteEntitySceneInternalBaking(world.EntityManager, settings.SceneGUID, scene.name, settings.AssetImportContext, sectionRefObjs, writeEntitySettings);

            if (disposeBlobAssetCache)
                settings.BlobAssetStore.Dispose();

            return sections;
        }

        internal static void RemoveBakingOnlyTypes(EntityManager entityManager)
        {
            // Removing EntityGUID as well as Baking Types
            var entityGUIDTypeIndex = TypeManager.GetTypeIndex(typeof(EntityGuid));
            var allTempBakingTypes = TypeManager.AllTypes.Where(t => (t.BakingOnlyType || t.TypeIndex == entityGUIDTypeIndex)).ToArray();
            for (int i = 0; i < allTempBakingTypes.Length; i++)
            {
                var componentType = ComponentType.FromTypeIndex(allTempBakingTypes[i].TypeIndex);
                EntityQueryDesc desc = new EntityQueryDesc()
                {
                    All = new [] {componentType},
                    Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab
                };
                using (var query = entityManager.CreateEntityQuery(desc))
                {
                    entityManager.RemoveComponent(query, componentType);
                }
            }
        }

        internal static SceneSectionData[] WriteEntitySceneInternalBaking(EntityManager entityManager, Hash128 sceneGUID,
            string sceneName, AssetImportContext importContext, List<ReferencedUnityObjects> sectionRefObjs, WriteEntitySceneSettings writeEntitySceneSettings)
        {
            RemoveBakingOnlyTypes(entityManager);

            using (var allTypes = new NativeParallelHashMap<ComponentType, int>(100, Allocator.Temp))
            using (var archetypes = new NativeList<EntityArchetype>(Allocator.Temp))
            {
                entityManager.GetAllArchetypes(archetypes);
                for (int i = 0; i < archetypes.Length; i++)
                {
                    var archetype = archetypes[i];
                    unsafe
                    {
                        if (archetype.Archetype->EntityCount == 0)
                            continue;
                    }

                    using (var componentTypes = archetype.GetComponentTypes())
                        foreach (var componentType in componentTypes)
                            if (allTypes.TryAdd(componentType, 0))
                            {
                                if(importContext != null)
                                    TypeDependencyCache.AddComponentTypeDependency(importContext, componentType);
                            }
                }

                using (var types = allTypes.GetKeyArray(Allocator.Temp))
                {
                    WriteExportedTypes(importContext, writeEntitySceneSettings, sceneGUID, types.Select(t => TypeManager.GetTypeInfo(t.TypeIndex)));
                }
            }
            return WriteEntitySceneInternal(entityManager, sceneGUID, sceneName, importContext, sectionRefObjs, writeEntitySceneSettings);
        }

        internal static void WriteExportedTypes(AssetImportContext ctx, WriteEntitySceneSettings writeEntitySceneSettings, Hash128 sceneGUID, IEnumerable<TypeManager.TypeInfo> typeInfos)
        {
            if (typeInfos.Any())
            {
                string exportedTypesPath;
                if(ctx != null)
                    exportedTypesPath = GetExportedTypesPath(EntityScenesPaths.PathType.EntitiesExportedTypes, "", ctx);
                else
                    exportedTypesPath = GetExportedTypesPath(EntityScenesPaths.PathType.EntitiesExportedTypes, sceneGUID.ToString(), writeEntitySceneSettings.OutputPath);

                using (var writer = File.CreateText(exportedTypesPath))
                {
                    writer.WriteLine($"::Exported Types (by stable hash)::");
                    foreach (var typeInfo in typeInfos)
                    {
                        // Record exported types in a separate log file for debug purposes
                        writer.WriteLine($"0x{typeInfo.StableTypeHash:x16} - {typeInfo.StableTypeHash,22} - {typeInfo.Type.FullName}");
                    }
                }
            }
        }

        internal static string GetExportedTypesPath(EntityScenesPaths.PathType type, string sceneGUID, string outputPath)
        {
            return Path.Combine(outputPath, sceneGUID + "." + EntityScenesPaths.GetExtension(type));
        }

        internal static string GetExportedTypesPath(EntityScenesPaths.PathType type, string sceneGUID, AssetImportContext ctx)
        {
            return ctx.GetOutputArtifactFilePath(sceneGUID + "." + EntityScenesPaths.GetExtension(type));
        }

#endregion
        /// <summary> Obsolete. Use <see cref="Scene.isSubScene"/> instead.</summary>
        [Obsolete("IsEntitySubScene is deprecated, use Scene.isSubScene (RemovedAfter 2021-04-27)")]
        internal static bool IsEntitySubScene(Scene scene)
        {
            return scene.isSubScene;
        }

        static unsafe AABB GetBoundsAndRemove(EntityManager entityManager, EntityQuery query)
        {
            var bounds = MinMaxAABB.Empty;
            using (var allBounds = query.ToComponentDataArray<SceneBoundingVolume>(Allocator.TempJob))
            {
                foreach (var b in allBounds)
                    bounds.Encapsulate(b.Value);
            }

            // query requires SceneBoundingVolume and SceneSection; entities that only have these types should be
            // destroyed at this point.
            var emptyEntityArchetype =
                entityManager.CreateArchetype(typeof(SceneBoundingVolume), typeof(SceneSection));
            var pEmptyArchetype = emptyEntityArchetype.Archetype;
            var ecs = entityManager.GetCheckedEntityDataAccess()->EntityComponentStore;
            using (var entities = query.ToEntityArray(Allocator.TempJob))
            {
                foreach (var e in entities)
                {
                    var chunk = ecs->GetChunk(e);
                    var chunkArchetype = ecs->GetArchetype(chunk);
                    if (chunkArchetype == pEmptyArchetype)
                    {
                        entityManager.DestroyEntity(e);
                    }
                    else
                        entityManager.RemoveComponent<SceneBoundingVolume>(e);
                }
            }

            return bounds;
        }

        internal static string GetSceneWritePath(EntityScenesPaths.PathType type, string subsectionName, AssetImportContext ctx)
        {
            var prefix = string.IsNullOrEmpty(subsectionName) ? "" : subsectionName + ".";
            return ctx.GetOutputArtifactFilePath(prefix + EntityScenesPaths.GetExtension(type));
        }

        internal static string GetSceneWritePath(EntityScenesPaths.PathType type, string subsectionName, Hash128 sceneGUID, string outputPath)
        {
            var prefix = string.IsNullOrEmpty(subsectionName) ? "" : subsectionName + ".";
            return Path.Combine(outputPath, sceneGUID + "." + prefix + EntityScenesPaths.GetExtension(type));
        }

        internal static void GetSceneSections(EntityManager entityManager, Hash128 sceneGUID, ref List<SceneSection> sections)
        {
            entityManager.GetAllUniqueSharedComponentsManaged(sections);
            //Order sections by section id
            sections.Sort(Comparer<SceneSection>.Create((a, b) => a.Section.CompareTo(b.Section)));

            if (sceneGUID == default)
                throw new ArgumentException("sceneGUID may not be default value");

            {
                // test for, and remove, SceneSection instances with negative Section values.
                int s = 0;
                while (sections[s].Section < 0)
                {
                    var path = AssetDatabaseCompatibility.GuidToPath(sections[s].SceneGUID);
                    if (path == null)
                        path = "";
                    else
                        path = $"\"{path}\"";

                    UnityEngine.Debug.LogWarning($"Encountered SceneSection (sceneGUID {sections[s].SceneGUID} {path}) with invalid Section value {sections[s].Section}.  SceneSection Section values must be >= 0.  Associated entities will be ignored.");
                    s++;
                }

                if (s > 0)
                    sections.RemoveRange(0, s);
            }

            for (int s=1; s < sections.Count; ++s)
            {
                if (sections[s].SceneGUID != sceneGUID)
                    throw new ArgumentException($"sceneGUID ({sceneGUID}) must match SceneSectionGUID ({sections[s].SceneGUID})");
            }
        }

        static SceneSectionData[] WriteEntitySceneInternal(EntityManager entityManager, Hash128 sceneGUID,
            string sceneName, AssetImportContext importContext,
            List<ReferencedUnityObjects> sectionRefObjs, WriteEntitySceneSettings writeEntitySceneSettings)
        {
            var sceneSectionDataList = new List<SceneSectionData>();
            var sceneSectionBlobHeaders = new List<BlobAssetReference<DotsSerialization.BlobHeader>>();

            var prefabRoot = writeEntitySceneSettings.PrefabRoot;

            if (importContext != null)
                TypeDependencyCache.AddAllSystemsDependency(importContext);

            var subSectionList = new List<SceneSection>();
            GetSceneSections(entityManager, sceneGUID, ref subSectionList);

            var weakAssetRefs = new NativeParallelHashSet<UntypedWeakReferenceId>(16, Allocator.Persistent);

            NativeArray<Entity> entitiesInMainSection;

            var sectionQuery = entityManager.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] {ComponentType.ReadWrite<SceneSection>()},
                    Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities
                }
            );

            var sectionBoundsQuery = entityManager.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] {ComponentType.ReadWrite<SceneBoundingVolume>(), ComponentType.ReadWrite<SceneSection>()},
                    Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities
                }
            );

            // filter the queries to match only the main section
            {
                var mainSection = new SceneSection { SceneGUID = sceneGUID, Section = 0 };
                sectionQuery.SetSharedComponentFilterManaged(mainSection);
                sectionBoundsQuery.SetSharedComponentFilterManaged(mainSection);
            }

            // It is important to remove the bounds entities before generating the remap for the external references.
            // With entity store V2, since the bounds will be removed before the entities are moved,
            // their entity indices will likely be reused in the section world. This causes those
            // external entity remapping and actual entity remapping to conflict each other.
            // It doesn't affect entity store V1, except that removing the bounds before the move prevents invalid
            // external entities to be stored in the public references dynamic buffer (as nulls).
            var mainSectionBounds = GetBoundsAndRemove(entityManager, sectionBoundsQuery);

            entitiesInMainSection = sectionQuery.ToEntityArray(Allocator.TempJob);

            // These are placeholders for section 0 that will be replaced after the other sections have been saved.
            sceneSectionDataList.Add(default);
            sceneSectionBlobHeaders.Add(default);

            {
                // Index 0 is the default value of the shared component, not an actual section
                for (int subSectionIndex = 1; subSectionIndex < subSectionList.Count; ++subSectionIndex)
                {
                    var subSection = subSectionList[subSectionIndex];
                    if (subSection.Section == 0)
                        continue;

                    sectionQuery.SetSharedComponentFilterManaged(subSection);
                    sectionBoundsQuery.SetSharedComponentFilterManaged(subSection);

                    var bounds = GetBoundsAndRemove(entityManager, sectionBoundsQuery);

                    var entitiesInSection = sectionQuery.ToEntityArray(Allocator.TempJob);

                    if (entitiesInSection.Length > 0)
                    {
                        var entityRemapping = entityManager.CreateEntityRemapArray(Allocator.TempJob);

#if ENTITY_STORE_V1
                        // Entities will be remapped to a contiguous range in the section world, but they will
                        // also come with an unpredictable amount of meta entities. We have the guarantee that
                        // the entities in the main section won't be moved over, so there's a free range of that
                        // size at the end of the remapping table. So we use that range for external references.
                        var externEntityIndexStart = entityRemapping.Length - entitiesInMainSection.Length;
#endif

                        using var sectionWorld = new World("SectionWorld");
                        var sectionManager = sectionWorld.EntityManager;

                        // Insert mapping for external references, conversion world entity to virtual index in section
                        for (int i = 0; i < entitiesInMainSection.Length; ++i)
                        {
                            var src = entitiesInMainSection[i];
#if !ENTITY_STORE_V1
                            // With entity store V2 no remapping should occur on the external references.
                            // To prevent them from being reset to null (they're not part of the set being moved),
                            // we force them to map onto themselves.
                            var dst = src;
#else
                            var dst = new Entity { Index = i + externEntityIndexStart, Version = 1 };
#endif
                            EntityRemapUtility.AddEntityRemapping(ref entityRemapping, src, dst);
                        }

                        sectionManager.MoveEntitiesFrom(entityManager, sectionQuery, entityRemapping);
                        writeEntitySceneSettings.PrefabRoot = EntityRemapUtility.RemapEntity(ref entityRemapping, prefabRoot);

#if !ENTITY_STORE_V1
                        // We have to figure out the right remapping range for serializing external references.
                        // Knowing that actual entities in the section will be serialized sequentially
                        // as {0,1,2,3,...,N-1} it's only a matter of counting how many actual entities
                        // are in the section to infer that the external remapping range starts at N.

                        var externEntityIndexStart = 0;

                        unsafe
                        {
                            var entityDataAccess = sectionManager.GetCheckedEntityDataAccess();
                            var archetypes = entityDataAccess->EntityComponentStore->m_Archetypes;
                            for (int ai = 0, ac = archetypes.Length; ai < ac; ai++)
                            {
                                var chunks = archetypes[ai]->Chunks;
                                for (int ci = 0, cc = chunks.Count; ci < cc; ci++)
                                {
                                    var chunk = chunks[ci];
                                    externEntityIndexStart += chunk.Count;
                                }
                            }
                        }

                        // The remap array might not be large enough this time, because the new entities allocated
                        // in the section world can have larger indices than what the main world used.
                        // Beware that the remapping table should still accomodate the entities from the main section
                        // which might have still larger indices than what is now in the section.

                        var highestIndexInSectionWorld = sectionWorld.EntityManager.HighestEntityIndex();
                        if (highestIndexInSectionWorld + 1 > entityRemapping.Length)
                        {
                            entityRemapping.Dispose();
                            entityRemapping = CollectionHelper.CreateNativeArray<EntityRemapUtility.EntityRemapInfo>(highestIndexInSectionWorld + 1, Allocator.TempJob);
                        }

#else
                        // Now that all the required entities have been moved over, we can get rid of the gap between
                        // real entities and external references. This allows remapping during load to deal with a
                        // smaller remap table, containing only useful entries.

                        int highestEntityIndexInUse = 0;
                        for (int i = 0; i < externEntityIndexStart; ++i)
                        {
                            var targetIndex = entityRemapping[i].Target.Index;
                            if (targetIndex < externEntityIndexStart && targetIndex > highestEntityIndexInUse)
                                highestEntityIndexInUse = targetIndex;
                        }

                        var oldExternEntityIndexStart = externEntityIndexStart;
                        externEntityIndexStart = highestEntityIndexInUse + 1;
#endif

                        // When writing the scene, references to missing entities are set to Entity.Null by default
                        // (but only if they have been used, otherwise they remain untouched)
                        // We obviously don't want that to happen to our external references, so we add explicit mapping
                        // And at the same time, we put them back at the end of the effective range of real entities.
                        for (int i = 0; i < entitiesInMainSection.Length; ++i)
                        {
#if !ENTITY_STORE_V1
                            var src = entitiesInMainSection[i];
#else
                            var src = new Entity {Index = i + oldExternEntityIndexStart, Version = 1};
#endif
                            var dst = new Entity {Index = i + externEntityIndexStart, Version = 1};
                            EntityRemapUtility.AddEntityRemapping(ref entityRemapping, src, dst);
                        }

                        // The section component is only there to break the conversion world into different sections
                        // We don't want to store that on the disk
                        //@TODO: Component should be removed but currently leads to corrupt data file. Figure out why.
                        //sectionManager.RemoveComponent(sectionManager.UniversalQuery, typeof(SceneSection));

                        var writeResult = WriteEntitySceneSection(sectionManager, sceneGUID,
                            subSection.Section.ToString(), importContext, writeEntitySceneSettings, out var objectRefCount,
                            out var objRefs, weakAssetRefs, entityRemapping);
                        AddToListOrDestroy(sectionRefObjs, objRefs);

                        sceneSectionBlobHeaders.Add(writeResult.Header);

                        sceneSectionDataList.Add(new SceneSectionData
                        {
                            FileSize = writeResult.CompressedSize,
                            SceneGUID = sceneGUID,
                            ObjectReferenceCount = objectRefCount,
                            SubSectionIndex = subSection.Section,
                            BoundingVolume = bounds,
                            Codec = writeEntitySceneSettings.Codec,
                            DecompressedFileSize = writeResult.DecompressedSize,
                            ExternalEntitiesRefRange = entitiesInMainSection.Length,
                        });

                        entityRemapping.Dispose();
                    }

                    entitiesInSection.Dispose();
                }
            }

            {
                var section = new SceneSection {SceneGUID = sceneGUID, Section = 0};

                sectionQuery.SetSharedComponentFilterManaged(section);

                // Each section will be serialized in its own world, entities that don't have a section are part of the main scene.

                // Public references array, only on the main section.
                var refInfoEntity = entityManager.CreateEntity();
                entityManager.AddBuffer<PublicEntityRef>(refInfoEntity);
                entityManager.AddSharedComponentManaged(refInfoEntity, section);
                var publicRefs = entityManager.GetBuffer<PublicEntityRef>(refInfoEntity);

                for (int i = 0; i < entitiesInMainSection.Length; ++i)
                {
                    PublicEntityRef.Add(ref publicRefs,
                        new PublicEntityRef {entityIndex = i, targetEntity = entitiesInMainSection[i]});
                }

                UnityEngine.Debug.Assert(publicRefs.Length == entitiesInMainSection.Length);

                // Save main section
                using var sectionWorld = new World("SectionWorld");
                var sectionManager = sectionWorld.EntityManager;

                var entityRemapping = entityManager.CreateEntityRemapArray(Allocator.TempJob);
                sectionManager.MoveEntitiesFrom(entityManager, sectionQuery, entityRemapping);
                writeEntitySceneSettings.PrefabRoot = EntityRemapUtility.RemapEntity(ref entityRemapping, prefabRoot);

                // The section component is only there to break the conversion world into different sections
                // We don't want to store that on the disk
                //@TODO: Component should be removed but currently leads to corrupt data file. Figure out why.
                //sectionManager.RemoveComponent(sectionManager.UniversalQuery, typeof(SceneSection));

                var writeResult = WriteEntitySceneSection(sectionManager, sceneGUID, "0",
                    importContext, writeEntitySceneSettings, out var objectRefCount, out var objRefs, weakAssetRefs);

                AddToListOrDestroy(sectionRefObjs, objRefs);
                sceneSectionBlobHeaders[0] = writeResult.Header;

                sceneSectionDataList[0] = new SceneSectionData
                {
                    FileSize = writeResult.CompressedSize,
                    SceneGUID = sceneGUID,
                    ObjectReferenceCount = objectRefCount,
                    SubSectionIndex = 0,
                    BoundingVolume = mainSectionBounds,
                    Codec = writeEntitySceneSettings.Codec,
                    DecompressedFileSize = writeResult.DecompressedSize
                };

                entityRemapping.Dispose();
            }

            // Save the new header
            var sceneSectionsArray = sceneSectionDataList.ToArray();
            WriteSceneHeader(sceneGUID, sceneSectionsArray, sceneName, importContext, entityManager, writeEntitySceneSettings, sceneSectionBlobHeaders);

            WriteWeakAssetRefs(weakAssetRefs, sceneGUID, importContext, writeEntitySceneSettings);

            foreach (var blobHeader in sceneSectionBlobHeaders)
            {
                blobHeader.Dispose();
            }

            weakAssetRefs.Dispose();
            sectionQuery.Dispose();
            sectionBoundsQuery.Dispose();
            entitiesInMainSection.Dispose();

            return sceneSectionsArray;
        }

        static void AddToListOrDestroy(List<ReferencedUnityObjects> sectionRefObjs, ReferencedUnityObjects objRefs)
        {
            if (objRefs != null)
                sectionRefObjs?.Add(objRefs);
            else
                UnityObject.DestroyImmediate(objRefs);
        }

        /// <summary>
        /// Write the Entity Scene to binary files
        /// </summary>
        /// <param name="scene">The EntityManager of the scene to write to binary files</param>
        /// <param name="binaryPath">The path for writing the entities</param>
        /// <param name="objectReferencesPath">The path for writing the objects</param>
        internal static void Write(EntityManager scene, string binaryPath, string objectReferencesPath)
        {
            // Write binary entity file
            WriteEntityBinary(scene, out var objRefs, default, binaryPath, default, new WriteEntitySceneSettings());
            WriteObjectReferences(objRefs, objectReferencesPath);
        }

        /// <summary>
        /// Read an Entity Scene from binary files
        /// </summary>
        /// <param name="scene">The EntityManager of the scene to read from binary files</param>
        /// <param name="binaryPath">The path for reading the entities</param>
        /// <param name="objectReferencesPath">The path for reading the objects</param>
        internal static void Read(EntityManager scene, string binaryPath, string objectReferencesPath)
        {
            ReferencedUnityObjects referencedUnityObjects = null;
            if (File.Exists(objectReferencesPath))
            {
                var resourceRequests = UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(objectReferencesPath);
                referencedUnityObjects = (ReferencedUnityObjects)resourceRequests[0];
            }

            using (var reader = new StreamBinaryReader(binaryPath))
                SerializeUtilityHybrid.Deserialize(scene, reader, referencedUnityObjects);

            UnityObject.DestroyImmediate(referencedUnityObjects);
        }

        internal struct EntitySectionWriteResult
        {
            internal int DecompressedSize;
            internal int CompressedSize;
            internal BlobAssetReference<DotsSerialization.BlobHeader> Header;
        }

        internal static EntitySectionWriteResult WriteEntitySceneSection(EntityManager scene, Hash128 sceneGUID, string subsection,
            AssetImportContext importContext, WriteEntitySceneSettings writeEntitySceneSettings, out int objectReferenceCount, out ReferencedUnityObjects objRefs,
            NativeParallelHashSet<UntypedWeakReferenceId> weakAssetRefs,
            NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapInfos = default)
        {
            k_ProfileEntitiesSceneSave.Begin();

            string entitiesBinaryPath, objRefsPath;
            if (importContext != null)
            {
                entitiesBinaryPath = GetSceneWritePath(EntityScenesPaths.PathType.EntitiesBinary, subsection, importContext);
                objRefsPath = GetSceneWritePath(EntityScenesPaths.PathType.EntitiesUnityObjectReferences, subsection, importContext);
            }
            else
            {
                Assertions.Assert.IsNotNull(writeEntitySceneSettings.OutputPath, "If an AssetImportContext is not provided, a valid WriteEntitySceneSettings.OutputPath must be passed. Both are currently null");
                entitiesBinaryPath = GetSceneWritePath(EntityScenesPaths.PathType.EntitiesBinary, subsection, sceneGUID, writeEntitySceneSettings.OutputPath);
                objRefsPath = GetSceneWritePath(EntityScenesPaths.PathType.EntitiesUnityObjectReferences, subsection, sceneGUID, writeEntitySceneSettings.OutputPath);
            }
            objectReferenceCount = 0;

            // Write binary entity file
            var (decompressedSize, compressedSize, blobHeader) = WriteEntityBinary(scene, out objRefs, entityRemapInfos, entitiesBinaryPath, weakAssetRefs, writeEntitySceneSettings, true);
            objectReferenceCount = WriteObjectReferences(objRefs, objRefsPath);

            k_ProfileEntitiesSceneSave.End();
            return new EntitySectionWriteResult
            {
                CompressedSize = compressedSize,
                DecompressedSize = decompressedSize,
                Header = blobHeader
            };
        }

        static int WriteObjectReferences(ReferencedUnityObjects objRefs, string objRefsPath)
        {
            if (objRefs == null || objRefs.Array.Length == 0)
                return 0;

            var companionObjectIndices = new List<int>();

            // Write object references
            using (k_ProfileEntitiesSceneWriteObjRefs.Auto())
            {
                var serializedObjectList = new List<UnityObject>();
                serializedObjectList.Add(objRefs);

                for (int i = 0; i != objRefs.Array.Length; i++)
                {
                    var obj = objRefs.Array[i];
                    if (obj != null && !EditorUtility.IsPersistent(obj))
                    {
                        if (obj is GameObject gameObject)
                        {
                            // Reset hide flags, otherwise they would prevent putting hybrid components in builds.
                            gameObject.hideFlags = HideFlags.None;
                            foreach (var component in gameObject.GetComponents<UnityEngine.Component>())
                                component.hideFlags = HideFlags.None;

                            serializedObjectList.Add(gameObject);
                            serializedObjectList.AddRange(gameObject.GetComponents<UnityEngine.Component>());

                            // Add companion entry, this allows us to differentiate Prefab references and Companion Objects at runtime deserialization
                            companionObjectIndices.Add(i);

                            continue;
                        }

                        if (obj is UnityEngine.Component)
                            continue;

                        if ((obj.hideFlags & HideFlags.DontSaveInBuild) == 0)
                            serializedObjectList.Add(obj);
                        else
                            objRefs.Array[i] = null;
                    }
                }

                objRefs.CompanionObjectIndices = companionObjectIndices.ToArray();

                UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(serializedObjectList.ToArray(), objRefsPath, false);

                return objRefs.Array.Length;
            }
        }

        private static unsafe (int decompressedSize, int compressedSize, BlobAssetReference<DotsSerialization.BlobHeader>)
            WriteEntityBinary(EntityManager scene, out ReferencedUnityObjects objRefs, NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapInfos, string entitiesBinaryPath, NativeParallelHashSet<UntypedWeakReferenceId> weakAssetRefs,
            WriteEntitySceneSettings writeEntitySceneSettings,  bool buildBlobHeader = false)
        {
            BlobAssetReference<DotsSerialization.BlobHeader> blobHeader = default;
            SerializeUtility.Settings serializeSetting = default;
            serializeSetting.PrefabRoot = writeEntitySceneSettings.PrefabRoot;

            objRefs = null;
            int decompressedSize;
            int compressedSize;
            using (var writer = new StreamBinaryWriter(entitiesBinaryPath))
            using (var unityObjectRefs = new UnityObjectRefMap(Allocator.Temp))
            using (var entitiesWriter = new MemoryBinaryWriter())
            {
                var entityRemapInfosCreated = entityRemapInfos.IsCreated;
                if (!entityRemapInfosCreated)
                {
#if !ENTITY_STORE_V1
                    entityRemapInfos = new NativeArray<EntityRemapUtility.EntityRemapInfo>(scene.HighestEntityIndex() + 1, Allocator.Temp);
#else
                    entityRemapInfos = new NativeArray<EntityRemapUtility.EntityRemapInfo>(scene.EntityCapacity, Allocator.Temp);
#endif
                }

                blobHeader = SerializeUtility.SerializeWorldInternal(scene, entitiesWriter, unityObjectRefs,
                    entityRemapInfos, weakAssetRefs, serializeSetting, buildBlobHeader);

                SerializeUtilityHybrid.SerializeObjectReferences(unityObjectRefs.ToObjectArray(), out objRefs);

                if (!entityRemapInfosCreated)
                    entityRemapInfos.Dispose();

                decompressedSize = entitiesWriter.Length;
                compressedSize = decompressedSize;

                if (writeEntitySceneSettings.Codec != Codec.None)
                {
                    var allocatorType = Allocator.Temp;
                    compressedSize = CodecService.Compress(writeEntitySceneSettings.Codec, entitiesWriter.Data, entitiesWriter.Length,
                        out var compressedData, allocatorType);
                    writer.WriteBytes(compressedData, compressedSize);
                    Memory.Unmanaged.Free(compressedData, allocatorType);
                }
                else
                {
                    writer.WriteBytes(entitiesWriter.Data, entitiesWriter.Length);
                }
            }

            return (decompressedSize, compressedSize, blobHeader);
        }

        static void WriteWeakAssetRefs(NativeParallelHashSet<UntypedWeakReferenceId> weakAssetRefs, Hash128 sceneGUID, AssetImportContext ctx, WriteEntitySceneSettings writeEntitySceneSettings)
        {
            string path;
            if (ctx != null)
                path = GetSceneWritePath(EntityScenesPaths.PathType.EntitiesWeakAssetRefs, "", ctx);
            else
                path = GetSceneWritePath(EntityScenesPaths.PathType.EntitiesWeakAssetRefs, "", sceneGUID, writeEntitySceneSettings.OutputPath);

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BlobArray<UnsafeUntypedWeakReferenceId>>();
            var array = builder.Allocate(ref root, weakAssetRefs.Count());

            int i = 0;
            foreach (var asset in weakAssetRefs)
            {
                array[i++] = new UnsafeUntypedWeakReferenceId(asset);
            }

            BlobAssetReference<BlobArray<UnsafeUntypedWeakReferenceId>>.Write(builder, path, 1);

            builder.Dispose();
        }

        internal static unsafe void WriteSceneHeader(Hash128 sceneGUID, SceneSectionData[] sections, string sceneName,
            AssetImportContext ctx, EntityManager entityManager, WriteEntitySceneSettings writeEntitySceneSettings,
            List<BlobAssetReference<DotsSerialization.BlobHeader>> sceneSectionBlobHeaders)
        {
            k_ProfileEntitiesSceneSaveHeader.Begin();

            string headerPath;
            if (ctx != null)
                headerPath = GetSceneWritePath(EntityScenesPaths.PathType.EntitiesHeader, "", ctx);
            else
                headerPath = GetSceneWritePath(EntityScenesPaths.PathType.EntitiesHeader, "", sceneGUID, writeEntitySceneSettings.OutputPath);

            var builder = new BlobBuilder(Allocator.TempJob);
            ref var metaData = ref builder.ConstructRoot<SceneMetaData>();
            var sceneSectionDataArray = builder.Construct(ref metaData.Sections, sections);
            builder.AllocateString(ref metaData.SceneName, sceneName);

            SerializeSceneSectionCustomMetadata(sections, ref metaData, builder, sceneName, entityManager);
            long headerSize = 0;
            using (var writer = new StreamBinaryWriter(headerPath))
            {
                var blobAssetPtrs = new NativeArray<BlobAssetPtr>(sceneSectionBlobHeaders.Count, Allocator.Temp);
                for (int i = 0; i < sceneSectionBlobHeaders.Count; ++i)
                {
                    blobAssetPtrs[i] = sceneSectionBlobHeaders[i].ToBlobAssetPtr();
                }
                var totalBlobAssetBatchSize = SerializeUtility.CalculateBlobAssetBatchTotalSize(blobAssetPtrs, out var blobAssetOffsets);

                for (int i = 0; i < sceneSectionDataArray.Length; ++i)
                {
                    sceneSectionDataArray[i].BlobHeader.m_BlobAssetRefStorage = blobAssetOffsets[i];
                }

                blobAssetOffsets.Dispose();

                metaData.HeaderBlobAssetBatchSize = totalBlobAssetBatchSize;
                BlobAssetReference<SceneMetaData>.Write(writer, builder, SceneMetaDataSerializeUtility.CurrentFileFormatVersion);

                SerializeUtility.WriteBlobAssetBatch(writer, blobAssetPtrs, totalBlobAssetBatchSize);
                headerSize = writer.Position;
                blobAssetPtrs.Dispose();
            }

            builder.Dispose();

            if (headerSize > SerializeUtility.MaxSubsceneHeaderSize)
            {
                string errorMessage =
                    $"Entity scene header of scene '{sceneName}' is too large. Size = {headerSize}, Maximum size = {SerializeUtility.MaxSubsceneHeaderSize}. Decrease the number of SubScenes in '{sceneName}'.";
                Debug.LogError(errorMessage);
                File.Delete(headerPath);
            }
            k_ProfileEntitiesSceneSaveHeader.End();
        }

        internal static unsafe void WriteHeader(string headerPath, ref SceneMetaData metaData, BlobBuilderArray<SceneSectionData> sectionDataArray, BlobBuilder builder)
        {
            var sectionCount = sectionDataArray.Length;
            using (var writer = new StreamBinaryWriter(headerPath))
            {
                var blobAssetPtrs = new NativeArray<BlobAssetPtr>(sectionCount, Allocator.Temp);
                for (int i = 0; i < sectionCount; ++i)
                {
                    var blobAssetRef = ((BlobAssetReference<DotsSerialization.BlobHeader>) sectionDataArray[i].BlobHeader);
                    blobAssetPtrs[i] = blobAssetRef.ToBlobAssetPtr();

                }
                var totalBlobAssetBatchSize = SerializeUtility.CalculateBlobAssetBatchTotalSize(blobAssetPtrs, out var blobAssetOffsets);

                for (int i = 0; i < sectionCount; ++i)
                {
                    sectionDataArray[i].BlobHeader.m_BlobAssetRefStorage = blobAssetOffsets[i];
                }

                blobAssetOffsets.Dispose();

                metaData.HeaderBlobAssetBatchSize = totalBlobAssetBatchSize;
                BlobAssetReference<SceneMetaData>.Write(writer, builder, SceneMetaDataSerializeUtility.CurrentFileFormatVersion);

                SerializeUtility.WriteBlobAssetBatch(writer, blobAssetPtrs, totalBlobAssetBatchSize);
                blobAssetPtrs.Dispose();
            }
        }

        private static void SerializeSceneSectionCustomMetadata(SceneSectionData[] sections, ref SceneMetaData metaData,
            BlobBuilder builder, string sceneName, EntityManager entityManager)
        {
            var metaDataArray = builder.Allocate(ref metaData.SceneSectionCustomMetadata, sections.Length);
            EntityQuery sectionEntityQuery = default;
            for (int i = 0; i < sections.Length; ++i)
            {
                var sectionEntity = SerializeUtility.GetSceneSectionEntity(sections[i].SubSectionIndex, entityManager, ref sectionEntityQuery, false);
                if (sectionEntity != Entity.Null)
                    SerializeSceneSectionCustomMetadata(sectionEntity, ref metaDataArray[i], builder, sections[i], sceneName, entityManager);
            }
        }

        private static unsafe void SerializeSceneSectionCustomMetadata(Entity sectionEntity, ref BlobArray<SceneSectionCustomMetadata> metaDataSectionArray,
            BlobBuilder builder, SceneSectionData sectionData, string sceneName, EntityManager entityManager)
        {
            var types = entityManager.GetComponentTypes(sectionEntity);
            int componentCount = 0;
            for (int i = 0; i < types.Length; ++i)
            {
                var type = types[i];
                if (type == ComponentType.ReadWrite<SectionMetadataSetup>())
                    continue;
                ref readonly var typeInfo = ref TypeManager.GetTypeInfo(type.TypeIndex);
                bool simpleComponentData = !type.IsManagedComponent && !type.IsCleanupComponent && typeInfo.Category == TypeManager.TypeCategory.ComponentData;
                if (!simpleComponentData || typeInfo.EntityOffsetCount > 0 || typeInfo.BlobAssetRefOffsetCount > 0)
                {
                    string Amount(int value) => $"{(value == 1 ? "is" : "are")} {value}";

                    var reasons = new[]
                    {
                        $"must be unmanaged ({(type.IsManagedComponent ? "it is managed" : "it is unmanaged")})",
                        $"must not implement {nameof(ICleanupComponentData)} ({(type.IsCleanupComponent ? "it does" : "it doesn't")})",
                        $"must implement {nameof(IComponentData)} ({(typeInfo.Category == TypeManager.TypeCategory.ComponentData ? "it does" : "it doesn't")})",
                        $"may not have any {nameof(Entity)} fields (there {Amount(typeInfo.EntityOffsetCount)})",
                        $"and may not have any BlobAssetReference fields (there {Amount(typeInfo.BlobAssetRefOffsetCount)})"
                    };

                    UnityEngine.Debug.LogError(
                        $"Can't serialize Custom Metadata {typeInfo.Type.Name} of SceneSection {sectionData.SubSectionIndex} of SubScene {sceneName}. " +
                        $"SubScene section entities may only have components that satisfy the following conditions: {string.Join(", ", reasons)}.");
                    continue;
                }
                types[componentCount++] = type;
            }
            var metadataArray = builder.Allocate(ref metaDataSectionArray, componentCount);
            for (int i = 0; i < componentCount; ++i)
            {
                var typeIndex = types[i].TypeIndex;
                ref readonly var typeInfo = ref TypeManager.GetTypeInfo(typeIndex);
                metadataArray[i].StableTypeHash = typeInfo.StableTypeHash;
                if (types[i].IsZeroSized)
                    continue;

                var componentData = entityManager.GetComponentDataRawRO(sectionEntity, typeIndex);
                var data = builder.Allocate(ref metadataArray[i].Data, typeInfo.TypeSize);
                UnsafeUtility.MemCpy(data.GetUnsafePtr(), componentData, typeInfo.TypeSize);
            }
            types.Dispose();
        }

        /// <summary>
        /// Takes dependencies on the format version of the Entity Binary File, on the Entity Scene Dependency file and the BuildConfiguration
        /// </summary>
        /// <param name="ctx">The AssetImportContext to use for taking the dependency</param>
        /// <param name="buildConfigurationGUID">The GUID of the build configuration</param>
        internal static void AddEntityBinaryFileDependencies(AssetImportContext ctx, Hash128 buildConfigurationGUID)
        {
            ctx.DependsOnCustomDependency("EntityBinaryFileFormatVersion");
            ctx.DependsOnSourceAsset(EntitiesCacheUtility.globalEntitySceneDependencyPath);

            //@TODO: This really needs to be way more precise.
            //       When conversion code accesses a specific component it should depend on that specific piece of data
            //       (Eg. adding a scene shouldn't invalidate all cached scenes...)
            if (buildConfigurationGUID.IsValid)
                ctx.DependsOnArtifact(buildConfigurationGUID);
        }

        internal static void DependOnSceneGameObjects(GUID sceneGUID, AssetImportContext context)
        {
            // Depend on with guid
            context.DependsOnSourceAsset(sceneGUID);

            // Do this guid based...
            //@TODO: Expose a method to find all actual PrefabInstances, instead of this path based hack
            var dependencies = AssetDatabase.GetDependencies(AssetDatabaseCompatibility.GuidToPath(sceneGUID));
            foreach (var dependency in dependencies)
            {
                if (!dependency.ToLower().EndsWith(".unity", StringComparison.Ordinal))
                {
                    GUID dependencyGUID = AssetDatabaseCompatibility.PathToGUID(dependency);
                    context.DependsOnArtifact(dependencyGUID);
                }
            }
        }

        /// <summary>
        /// Gets all the sub scenes embedded in a scene.
        /// </summary>
        /// <param name="guid">The GUID of the scene</param>
        /// <returns>A list of all sub scenes embedded in the given scene</returns>
        public static Hash128[] GetSubScenes(GUID guid)
        {
            return GameObjectSceneMetaDataImporter.GetSubScenes(guid);
        }
    }
}
