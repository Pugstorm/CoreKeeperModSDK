using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Build;
using Unity.Entities.Conversion;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes.Editor
{
    //@TODO: Live link currently force loads all sections. It should only live link sections that are marked for loading.
    class LiveConversionDiffGenerator : IDisposable
    {
        World                       _GameObjectWorld;

        World                       _ConvertedWorld;
        EntityQuery                 _MissingRenderDataQuery;
        EntityQuery                 _MissingSceneQuery;
        EntityManagerDiffer         _LiveConversionDiffer;

        EntityDiffer.CachedComponentChanges _CachedComponentChanges;
        IncrementalConversionDebug _IncrementalConversionDebug;
        IncrementalBakingChangeTracker     _IncrementalBakingChangeTracker;
        bool                        _RequestCleanConversion;
        bool                        _LiveConversionEnabled;

        Scene                       _Scene;
        GUID                        _configGUID;
        IEntitiesPlayerSettings     _settingAsset;
        readonly Hash128            _SceneGUID;
        readonly BlobAssetStore     _BlobAssetStore;
        ulong                       _ArtifactDependencyVersion;

        struct IncrementalConversionDebug
        {
            public World           World;
            public EntityQuery     MissingRenderDataQuery;
            public EntityQuery     MissingSceneQuery;
            public bool            NeedsUpdate;
            public BakingUtility.BakingFlags LastBakingFlags;

            public void Dispose()
            {
                if (World == null)
                    return;
                MissingRenderDataQuery.Dispose();
                MissingSceneQuery.Dispose();
                World.Dispose();
            }
        }

        internal IncrementalBakingChangeTracker ChangeTracker => _IncrementalBakingChangeTracker;

        public void RequestCleanConversion()
        {
            _RequestCleanConversion = true;
        }

        public bool DidRequestUpdate(uint artifactDependencyVersion)
        {
            var didChange = _RequestCleanConversion || _IncrementalBakingChangeTracker.HasAnyChanges() || _ArtifactDependencyVersion != artifactDependencyVersion;

            // if (didChange)
            //     Debug.Log($"DidRequestUpdate. Clean: {_RequestCleanConversion} HasChanges: {_IncrementalConversionChangeTracker.HasAnyChanges()} ArtifactDependencyVersion: {_ArtifactDependencyVersion != artifactDependencyVersion}");

            return didChange;
        }

        LiveConversionDiffGenerator(Scene scene, Hash128 sceneGUID, GUID configGUID, IEntitiesPlayerSettings settingAsset, bool liveConversionEnabled)
        {
            _SceneGUID = sceneGUID;
            _Scene = scene;
            _configGUID = configGUID;
            _settingAsset = settingAsset;

            _LiveConversionEnabled = liveConversionEnabled;

            ConstructConversionWorld($"Converted Scene: '{_Scene.name}'", ref _ConvertedWorld, ref _MissingRenderDataQuery, ref _MissingSceneQuery);
            _BlobAssetStore = new BlobAssetStore(128);

            var queryDesc = new EntityQueryDesc
            {
                All = new[] {ComponentType.ReadOnly<EntityGuid>()},
                None = new[] {ComponentType.ReadOnly<RemoveUnusedEntityInBake>(), ComponentType.ReadOnly<BakingOnlyEntity>()},
                Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab
            };

            _LiveConversionDiffer = new EntityManagerDiffer(_ConvertedWorld.EntityManager, Allocator.Persistent, queryDesc);
            _IncrementalBakingChangeTracker = new IncrementalBakingChangeTracker();

            //@TODO: DOTS-5455

            _RequestCleanConversion = true;
            _CachedComponentChanges = new EntityDiffer.CachedComponentChanges(1024);
        }

        void ConstructConversionWorld(string name, ref World world, ref EntityQuery missingRenderDataQuery, ref EntityQuery missingSceneQuery)
        {
            var worldFlags = WorldFlags.Editor | WorldFlags.Conversion | WorldFlags.Staging;
            world = new World(name, worldFlags);

            var missingRenderDataQueryDesc = new EntityQueryDesc
            {
                All = new ComponentType[] { typeof(SceneTag) },
                None = new ComponentType[] { typeof(EditorRenderData) },
                Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities
            };
            var missingSceneDataQueryDesc = new EntityQueryDesc
            {
                None = new ComponentType[] { typeof(SceneTag) },
                Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities
            };

            missingRenderDataQuery = world.EntityManager.CreateEntityQuery(missingRenderDataQueryDesc);
            missingSceneQuery = world.EntityManager.CreateEntityQuery(missingSceneDataQueryDesc);
        }

        void LazyCreateIncrementalConversionDebug()
        {
            if (_IncrementalConversionDebug.World == null)
                ConstructConversionWorld($"Incremental Conversion Debug: '{_Scene.name}'", ref _IncrementalConversionDebug.World, ref _IncrementalConversionDebug.MissingRenderDataQuery, ref _IncrementalConversionDebug.MissingSceneQuery);
        }

        public void Dispose()
        {
            _BlobAssetStore.Dispose();
            _IncrementalBakingChangeTracker.Dispose();
            _IncrementalConversionDebug.Dispose();
            _CachedComponentChanges.Dispose();

            try
            {
                _LiveConversionDiffer.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            try
            {
                if (_GameObjectWorld != null && _GameObjectWorld.IsCreated)
                    _GameObjectWorld.Dispose();
                _GameObjectWorld = null;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            try
            {
                if (_ConvertedWorld != null && _ConvertedWorld.IsCreated)
                    _ConvertedWorld.Dispose();
                _ConvertedWorld = null;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        static readonly ProfilerMarker LiveConversionConvertMarker = new ProfilerMarker("LiveConversion.Convert");
        static readonly ProfilerMarker IncrementalConversionMarker = new ProfilerMarker("IncrementalConversion");
        static readonly ProfilerMarker DebugConversionMarker = new ProfilerMarker("DebugConversion");
        static readonly ProfilerMarker CleanConversionMarker = new ProfilerMarker("CleanConversion");
        static readonly ProfilerMarker BlobAssetStoreGarbageCollection = new ProfilerMarker("BlobAssetStoreGarbageCollection");

        internal World ConvertedWorld => _ConvertedWorld;

        bool Bake(BakingUtility.BakingFlags flags)
        {
            using (LiveConversionConvertMarker.Auto())
            {
                var mode = LiveConversionSettings.Mode;
                if (mode == LiveConversionSettings.ConversionMode.AlwaysCleanConvert)
                    _RequestCleanConversion = true;

                _IncrementalConversionDebug.LastBakingFlags = flags;
                _IncrementalConversionDebug.NeedsUpdate = !_RequestCleanConversion;

                var conversionSettings = GetBakeSettings(flags, _BlobAssetStore, _settingAsset);
                var didBake = BakingUtility.BakeScene(_ConvertedWorld, _Scene, conversionSettings, !_RequestCleanConversion, _IncrementalBakingChangeTracker);
                if (didBake)
                    AddMissingData(_ConvertedWorld, _MissingSceneQuery, _MissingRenderDataQuery, flags);

                using (BlobAssetStoreGarbageCollection.Auto())
                {
                    _BlobAssetStore.GarbageCollection(_ConvertedWorld.EntityManager);
                }

                _IncrementalBakingChangeTracker.Clear();
                _RequestCleanConversion = false;
                return didBake;
            }
        }

        BakingSettings GetBakeSettings(BakingUtility.BakingFlags flags, BlobAssetStore store, IEntitiesPlayerSettings settingAssets)
        {
            var conversionSettings = new BakingSettings(flags, store)
            {
                SceneGUID = _SceneGUID,
                DotsSettings = settingAssets,
            };
            if (LiveConversionSettings.AdditionalConversionSystems.Count != 0)
                conversionSettings.ExtraSystems.AddRange(LiveConversionSettings.AdditionalConversionSystems);
            return conversionSettings;
        }


        internal bool DidRequestDebugConversion() => _IncrementalConversionDebug.NeedsUpdate;

        internal void DebugIncrementalConversion()
        {
            if (!_IncrementalConversionDebug.NeedsUpdate)
                return;

            LazyCreateIncrementalConversionDebug();

            _IncrementalConversionDebug.NeedsUpdate = false;
            using (DebugConversionMarker.Auto())
            {
                using var blobAssetStore = new BlobAssetStore(128);

                var flags = _IncrementalConversionDebug.LastBakingFlags;

                // We need to skip creating companions in the debug conversion pass
                flags |= BakingUtility.BakingFlags.SkipCreatingCompanions;

                // use this to compare the results of incremental conversion with the results of a clean conversion.
                var conversionSettings = GetBakeSettings(flags, blobAssetStore, _settingAsset);
                BakingUtility.BakeScene(_IncrementalConversionDebug.World, _Scene, conversionSettings, false, null);

                AddMissingData(_IncrementalConversionDebug.World, _IncrementalConversionDebug.MissingSceneQuery,
                    _IncrementalConversionDebug.MissingRenderDataQuery, flags);
                const EntityManagerDifferOptions options =
                    EntityManagerDifferOptions.IncludeForwardChangeSet |
                    EntityManagerDifferOptions.ValidateUniqueEntityGuid |
                    EntityManagerDifferOptions.UseReferentialEquality;

                using var blobAssetCache = new BlobAssetCache(Allocator.Persistent);
                EntityDiffer.PrecomputeBlobAssetCache(_ConvertedWorld.EntityManager,
                    EntityManagerDiffer.EntityGuidQueryDesc, blobAssetCache);

                var changes = EntityDiffer.GetChanges(
                    ref _CachedComponentChanges,
                    _IncrementalConversionDebug.World.EntityManager,
                    _ConvertedWorld.EntityManager,
                    options,
                    EntityManagerDiffer.EntityGuidQueryDesc,
                    blobAssetCache,
                    Allocator.TempJob
                );
                using (changes)
                {
                    if (!changes.AnyChanges)
                        return;

                    var fwdChanges = changes.ForwardChangeSet;
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                    {

                        // Remove all companion link object changes.
                        // Companion objects will always be different between different conversions, so this is
                        // absolutely expected.
                        // It is unlikely that a diff will ever only consist of changes to hybrid components, and even
                        // in that case pointing out that the companion link changed is not exactly helpful for the user
                        // either.
                        var managedComponents = fwdChanges.SetManagedComponents;
                        var setComponents = fwdChanges.SetComponents;
                        var componentsRemoved = fwdChanges.RemoveComponents;
                        int numCompanionLinkObjects = 0;
                        var types = fwdChanges.TypeHashes;
                        var companionReferenceIndex = TypeManager.GetTypeIndex<CompanionReference>();
                        var companionLinkIndex = TypeManager.GetTypeIndex<CompanionLink>();
                        var companionLinkTransformIndex = TypeManager.GetTypeIndex<CompanionLinkTransform>();
                        int lastManaged = managedComponents.Length - 1;
                        for (int i = lastManaged; i >= 0; i--)
                        {
                            // We need to go through the type index to correctly handle null Companion Links
                            int packedTypeIdx = managedComponents[i].Component.PackedTypeIndex;
                            var idx = TypeManager.GetTypeIndexFromStableTypeHash(types[packedTypeIdx].StableTypeHash);
                            if (idx == companionReferenceIndex)
                            {
                                managedComponents[i] = managedComponents[lastManaged - numCompanionLinkObjects];
                                numCompanionLinkObjects += 1;
                            }
                        }

                        int lastComponentSet = setComponents.Length - 1;
                        int numCompanionComponentsSet = 0;
                        for (int i = lastComponentSet; i >= 0; i--)
                        {
                            // We need to go through the type index to correctly handle null Companion Links
                            int packedTypeIdx = setComponents[i].Component.PackedTypeIndex;
                            var idx = TypeManager.GetTypeIndexFromStableTypeHash(types[packedTypeIdx].StableTypeHash);
                            if (idx == companionLinkIndex || idx == companionLinkTransformIndex)
                            {
                                setComponents[i] = setComponents[lastComponentSet - numCompanionComponentsSet];
                                numCompanionComponentsSet += 1;
                            }
                        }

                        int lastComponentRemoved = componentsRemoved.Length - 1;
                        int numCompanionLinkComponentsRemoved = 0;
                        for (int i = lastComponentRemoved; i >= 0; i--)
                        {
                            // We need to go through the type index to correctly handle null Companion Links
                            int packedTypeIdx = componentsRemoved[i].PackedTypeIndex;
                            var idx = TypeManager.GetTypeIndexFromStableTypeHash(types[packedTypeIdx].StableTypeHash);
                            if (idx == companionLinkIndex || idx == companionLinkTransformIndex || idx == companionReferenceIndex)
                            {
                                componentsRemoved[i] = componentsRemoved[lastComponentRemoved - numCompanionLinkComponentsRemoved];
                                numCompanionLinkComponentsRemoved += 1;
                            }
                        }

                        if (numCompanionLinkObjects > 0 || numCompanionComponentsSet > 0 || numCompanionLinkComponentsRemoved > 0)
                        {
                            // throw away the companion link changes
                            Array.Resize(ref managedComponents, lastManaged + 1 - numCompanionLinkObjects);
                            var setComponentsWithoutCompanions = setComponents.GetSubArray(0, lastComponentSet + 1 - numCompanionComponentsSet);
                            var removeComponentsWithoutCompanions = componentsRemoved.GetSubArray(0, lastComponentRemoved + 1 - numCompanionLinkComponentsRemoved);
                            fwdChanges = new EntityChangeSet(fwdChanges.CreatedEntityCount,
                                fwdChanges.DestroyedEntityCount,
                                fwdChanges.NameChangedCount,
                                fwdChanges.Entities,
                                fwdChanges.TypeHashes,
                                fwdChanges.Names,
                                fwdChanges.NameChangedEntityGuids,
                                fwdChanges.AddComponents,
                                fwdChanges.AddArchetypes,
                                removeComponentsWithoutCompanions,
                                setComponentsWithoutCompanions,
                                fwdChanges.ComponentData,
                                fwdChanges.EntityReferenceChanges,
                                fwdChanges.BlobAssetReferenceChanges,
                                managedComponents, // <- this changes
                                fwdChanges.SetSharedComponents,
                                fwdChanges.UnmanagedSharedComponentData,
                                fwdChanges.LinkedEntityGroupAdditions,
                                fwdChanges.LinkedEntityGroupRemovals,
                                fwdChanges.CreatedBlobAssets,
                                fwdChanges.DestroyedBlobAssets,
                                fwdChanges.BlobAssetData);
                            if (!fwdChanges.HasChangesIncludeNames(false, true))
                                return;
                        }
                    }
#endif
                    _RequestCleanConversion = true;

                    EntityManager targetEntityManager = _IncrementalConversionDebug.World.EntityManager;
                    var sb = new StringBuilder();
                    fwdChanges.PrintSummary(targetEntityManager, sb);
                    var errorString =
                        "The result of incrementally converting changes and a clean conversion didn't match, are you missing some dependencies?\n" +
                        "This is what was added/removed/changed by the clean conversion relative to the incremental conversion:\n" +
                        sb;
                    if (LiveConversionSettings.TreatIncrementalConversionFailureAsError)
                        throw new Exception(errorString);
                    Debug.LogWarning(errorString);
                }
            }
        }

        static void AddMissingData(World world, EntityQuery missingSceneQuery, EntityQuery missingRenderDataQuery, BakingUtility.BakingFlags flags)
        {
            var em = world.EntityManager;
            // We don't know the scene tag of the destination world, so we create a null Scene Tag.
            // In the patching code this will be translated into the final scene entity.
            em.AddSharedComponentManaged(missingSceneQuery, new SceneTag { SceneEntity = Entity.Null });

            if((flags & BakingUtility.BakingFlags.SceneViewLiveConversion) == 0)
            {
                // if entities should not be rendered in Scene View, set the culling mask to Game View only
                // if the EditorRenderData is missing, the default culling is assumed (display in both Game View and Scene View)
                const ulong cullingMask = UnityEditor.SceneManagement.SceneCullingMasks.GameViewObjects;
                em.AddSharedComponentManaged(missingRenderDataQuery, new EditorRenderData { SceneCullingMask = cullingMask });
            }
        }

        public static bool UpdateLiveConversion(Scene scene, Hash128 sceneGUID, ref LiveConversionDiffGenerator liveConversionData, LiveConversionMode mode,
            ulong globalAsssetDependencyVersion, GUID configGUID, IEntitiesPlayerSettings settingAsset, out LiveConversionChangeSet changes)
        {
            // This should be removed or moved to a general setting
            int framesToRetainBlobAssets = 1;

            var liveConversionEnabled = mode != LiveConversionMode.Disabled;
            if (liveConversionData != null && liveConversionData._LiveConversionEnabled != liveConversionEnabled)
            {
                liveConversionData.Dispose();
                liveConversionData = null;
            }

            var unloadAllPreviousEntities = liveConversionData == null;
            // If conversion isn't enabled just make sure all entities get deleted and early out
            if (!liveConversionEnabled)
            {
                changes = new LiveConversionChangeSet
                {
                    UnloadAllPreviousEntities = unloadAllPreviousEntities,
                    SceneName = scene.name,
                    SceneGUID = sceneGUID,
                    FramesToRetainBlobAssets = framesToRetainBlobAssets
                };
                return true;
            }

            if (liveConversionData == null)
                liveConversionData = new LiveConversionDiffGenerator(scene, sceneGUID, configGUID, settingAsset, liveConversionEnabled);
            else if (liveConversionData._Scene != scene || liveConversionData._configGUID != configGUID)
            {
                liveConversionData._Scene = scene;
                liveConversionData._configGUID = configGUID;
                liveConversionData._RequestCleanConversion = true;
            }
            liveConversionData._ArtifactDependencyVersion = globalAsssetDependencyVersion;

            var flags = BakingUtility.BakingFlags.AddEntityGUID | BakingUtility.BakingFlags.AssignName | BakingUtility.BakingFlags.GameViewLiveConversion;
            if (mode == LiveConversionMode.SceneViewShowsRuntime)
                flags |= BakingUtility.BakingFlags.SceneViewLiveConversion;
            if (mode == LiveConversionMode.LiveConvertStandalonePlayer)
                flags |= BakingUtility.BakingFlags.IsBuildingForPlayer;
            var didBake = liveConversionData.Bake(flags);

            if (LiveConversionSettings.IsDebugLoggingEnabled)
                didBake = true;

            if (didBake)
            {
                const EntityManagerDifferOptions options =
                    EntityManagerDifferOptions.IncludeForwardChangeSet |
                    EntityManagerDifferOptions.FastForwardShadowWorld |
                    EntityManagerDifferOptions.ValidateUniqueEntityGuid |
                    EntityManagerDifferOptions.ClearMissingReferences;

                changes = new LiveConversionChangeSet
                {
                    Changes = liveConversionData._LiveConversionDiffer.GetChanges(options, Allocator.TempJob).ForwardChangeSet,
                    UnloadAllPreviousEntities = unloadAllPreviousEntities,
                    SceneName = scene.name,
                    SceneGUID = sceneGUID,
                    FramesToRetainBlobAssets = framesToRetainBlobAssets
                };

                // convertedEntityManager.Debug.CheckInternalConsistency();
                if (LiveConversionSettings.IsDebugLoggingEnabled)
                {
                    EntityManager targetEntityManager = liveConversionData._ConvertedWorld.EntityManager;
                    Debug.Log(changes.Changes.PrintSummary(targetEntityManager));
                }

                return true;
            }
            else
            {
                changes = default;
                return false;
            }
        }
    }
}
