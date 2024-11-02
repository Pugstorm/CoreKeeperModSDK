using System;
using System.IO;
using Unity.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes
{
    /// <summary>
    /// Component for public data interface with <see cref="SceneSystem"/>.
    /// </summary>
    public struct SceneSystemData : IComponentData
    {
        /// <summary>
        /// The GUID of the Unity.Build.BuildConfiguration applied on the <see cref="World"/>.
        /// </summary>
        public Hash128 BuildConfigurationGUID;
    }

    /// <summary>
    /// High level API for loading and unloading scenes
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor | WorldSystemFilterFlags.Streaming)]
    [UpdateInGroup(typeof(SceneSystemGroup))]
    [BurstCompile]
    public partial struct SceneSystem : ISystem, ISystemStartStop
    {
        /// <summary>
        /// Parameters for loading scenes.
        /// </summary>
        public struct LoadParameters
        {
            /// <summary>
            /// True if the <see cref="SceneLoadFlags.DisableAutoLoad"/> flag is set, otherwise false.
            /// </summary>
            public bool AutoLoad
            {
                get { return (Flags & SceneLoadFlags.DisableAutoLoad) == 0; }
                set => Flags = value ? Flags & ~SceneLoadFlags.DisableAutoLoad : Flags | SceneLoadFlags.DisableAutoLoad;
            }
            /// <summary>
            /// The flags applied when loading the scene.
            /// </summary>
            public SceneLoadFlags Flags;
            /// <summary>
            /// The priority of the load operation.
            /// </summary>
            public int Priority;
        }

        static internal RequestSceneLoaded CreateRequestSceneLoaded(LoadParameters loadParameters)
        {
            var requestSceneLoaded = new RequestSceneLoaded { LoadFlags = loadParameters.Flags};

            return requestSceneLoaded;
        }

        private EntityQuery _unloadSceneQuery;
        BlobAssetReference<ResourceCatalogData> catalogData;

        static internal string SceneLoadDir => Application.streamingAssetsPath;

        /// <summary>
        /// Callback invoked when the system starts running.
        /// </summary>
        /// <param name="state">The entity system state.</param>
        public void OnStartRunning(ref SystemState state)
        {
#if !UNITY_EDITOR
            LoadCatalogData(ref state);
#endif
        }

        /// <summary>
        /// Callback invoked when the system stops running.
        /// </summary>
        /// <param name="state">The entity system state.</param>
        public void OnStopRunning(ref SystemState state)
        {

        }


        /// <summary>
        /// Callback invoked when the system is created.
        /// </summary>
        /// <param name="state">The entity system state.</param>
        public void OnCreate(ref SystemState state)
        {
            _unloadSceneQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<SceneSectionStreamingSystem.StreamingState>()
                .WithNone<SceneEntityReference>()
                .Build(ref state);

            state.EntityManager.AddComponentData(state.SystemHandle, new SceneSystemData());
        }
        
        /// <summary>
        /// Callback invoked when the system is destroyed.
        /// </summary>
        /// <param name="state">The entity system state.</param>
        public void OnDestroy(ref SystemState state)
        {
            if (catalogData.IsCreated)
            {
                catalogData.Dispose();
            }
        }

        void LoadCatalogData(ref SystemState state)
        {
            var fullSceneInfoPath = EntityScenesPaths.FullPathForFile(SceneLoadDir, EntityScenesPaths.RelativePathForSceneInfoFile);
            if (FileUtilityHybrid.FileExists(fullSceneInfoPath))
            {
                if (!BlobAssetReference<ResourceCatalogData>.TryRead(fullSceneInfoPath, ResourceCatalogData.CurrentFileFormatVersion, out catalogData))
                {
                    Debug.LogError($"Unable to read catalog data from {fullSceneInfoPath}.");
                    return;
                }
            }
        }

        /// <summary>
        /// Get the guid for a scene path or name.  This is a slow method - it is best to use the guid directly.
        /// </summary>
        /// <param name="state">The entity system state.</param>
        /// <param name="scenePath">The scene path or name.</param>
        /// <returns>True if the scene guid exists.</returns>
        public static unsafe Hash128 GetSceneGUID(ref SystemState state, string scenePath)
        {
            // In future this should come from the active build configuration, or something along those lines
            // How will active build configuration work with Client AND Server in same process?
#if UNITY_EDITOR
            return AssetDatabaseCompatibility.PathToGUID(scenePath);
#else
            ref var cdata = ref ((SceneSystem*)state.m_SystemPtr)->catalogData;
            Assert.IsTrue(cdata.IsCreated, "The scene catalog has not been loaded yet");
            return (cdata.Value.GetGUIDFromPath(scenePath));
#endif
        }

        // TODO: Remove this when we have API to access scenes by GUID (Root Scene Conversion)
        // TODO: DOTS-3329
        internal static unsafe string GetScenePath(ref SystemState state, Hash128 sceneGUID)
        {
#if UNITY_EDITOR
            return AssetDatabaseCompatibility.GuidToPath(sceneGUID);
#else
            ref var cdata = ref ((SceneSystem*)state.m_SystemPtr)->catalogData;

            Assert.IsTrue(cdata.IsCreated, "The scene catalog has not been loaded yet");
            return cdata.Value.GetPathFromGUID(sceneGUID);
#endif
        }

        /// <summary>
        /// Check if a scene or subscene is loaded.
        /// </summary>
        /// <param name="world">The <see cref="World"/> in which the scene is loaded.</param>
        /// <param name="entity">The entity with the loading component data.  This is the entity returned by LoadSceneAsync.</param>
        /// <returns>True if the scene is loaded.</returns>
        public static bool IsSceneLoaded(WorldUnmanaged world, Entity entity)
        {
            if (!world.EntityManager.HasComponent<SceneReference>(entity))
                return false;

            if (!world.EntityManager.HasComponent<ResolvedSectionEntity>(entity))
                return false;

            var resolvedSectionEntities = world.EntityManager.GetBuffer<ResolvedSectionEntity>(entity);

            if (resolvedSectionEntities.Length == 0)
                return false;

            foreach (var s in resolvedSectionEntities)
            {
                if (!IsSectionLoaded(world, s.SectionEntity))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Contains the streaming state of a loading scene.
        /// </summary>
        public enum SceneStreamingState
        {
            /// <summary>
            /// The scene is not loading and is not expected to load. It could be that it has just been fully
            /// unloaded.
            /// </summary>
            Unloaded,
            /// <summary>
            /// The scene and section entities are loaded, but the content for the sections is not loaded or expected to load.
            /// </summary>
            LoadedSectionEntities,
            /// <summary>
            /// The scene still loading.
            /// </summary>
            Loading,
            /// <summary>
            /// The scene and all the requested sections loaded successfully.
            /// </summary>
            LoadedSuccessfully,
            /// <summary>
            /// The scene is currently unloading.
            /// </summary>
            Unloading,

            /// <summary>
            /// The scene failed to load the scene header.
            /// </summary>
            FailedLoadingSceneHeader,
            /// <summary>
            /// The scene finished loading, but at least one the sections failed to load successfully.
            /// </summary>
            LoadedWithSectionErrors,
        }

        /// <summary>
        /// Check the streaming state of a scene that is being loaded.
        /// </summary>
        /// <param name="world">The <see cref="World"/> in which the scene is loaded.</param>
        /// <param name="entity">The entity with the loading component data.  This is the entity returned by LoadSceneAsync.</param>
        /// <returns>The streaming state of the loading scene.</returns>
        public static SceneStreamingState GetSceneStreamingState(WorldUnmanaged world, Entity entity)
        {
            // Check if the entity is a Scene entity
            if (!world.EntityManager.HasComponent<SceneReference>(entity))
                return SceneStreamingState.Unloaded;

            DynamicBuffer<ResolvedSectionEntity> resolvedSectionEntities;

            // We check if we are loading the header
            if (world.EntityManager.HasComponent<RequestSceneLoaded>(entity) && !world.EntityManager.HasComponent<ResolvedSectionEntity>(entity) )
                return SceneStreamingState.Loading;

            resolvedSectionEntities = world.EntityManager.GetBuffer<ResolvedSectionEntity>(entity, true);
            if (resolvedSectionEntities.Length == 0)
                return SceneStreamingState.FailedLoadingSceneHeader;

            // We check the state of each section
            bool anyLoaded = false;
            bool anyFailed = false;
            foreach (var s in resolvedSectionEntities)
            {
                // If the streaming state is not there yet
                if (!world.EntityManager.HasComponent<SceneSectionStreamingSystem.StreamingState>(s.SectionEntity))
                {
                    // If RequestSceneLoaded is not there, it's not meant to load. We skip that section.
                    if (!world.EntityManager.HasComponent<RequestSceneLoaded>(s.SectionEntity))
                        continue; // This section is not loaded
                    return SceneStreamingState.Loading;
                }

                var sectionState = world.EntityManager.GetComponentData<SceneSectionStreamingSystem.StreamingState>(s.SectionEntity);
                switch (sectionState.Status)
                {
                    case SceneSectionStreamingSystem.StreamingStatus.Loading:
                    case SceneSectionStreamingSystem.StreamingStatus.NotYetProcessed:
                        return SceneStreamingState.Loading;
                    case SceneSectionStreamingSystem.StreamingStatus.FailedToLoad:
                        anyFailed = true;
                        break;
                    case SceneSectionStreamingSystem.StreamingStatus.Loaded:
                        anyLoaded = true;
                        break;
                }
            }

            // There was no section requested to load
            if (!anyLoaded && !anyFailed)
                return SceneStreamingState.LoadedSectionEntities;

            // We only return the error when all the sections that are requested to load are either loaded or failed to load
            if (anyFailed)
                return SceneStreamingState.LoadedWithSectionErrors;
            return SceneStreamingState.LoadedSuccessfully;
        }

        /// <summary>
        /// Contains the streaming state of a loading section.
        /// </summary>
        public enum SectionStreamingState
        {
            /// <summary>
            /// The section is not loading and it's not expected to load. It could be that it has just been fully
            /// unloaded.
            /// </summary>
            Unloaded,
            /// <summary>
            /// The section is expected to load, but the loading hasn't started yet. It could be waiting for section 0
            /// to load.
            /// </summary>
            LoadRequested,
            /// <summary>
            /// The section has been loaded.
            /// </summary>
            Loaded,
            /// <summary>
            /// The section currently is loading.
            /// </summary>
            Loading,
            /// <summary>
            /// The section has been marked for unloading but it hasn't been processed yet.
            /// </summary>
            UnloadRequested,
            /// <summary>
            /// The section failed to load.
            /// </summary>
            FailedToLoad,
        }

        /// <summary>
        /// Check the streaming state of a section that is being loaded.
        /// </summary>
        /// <param name="world">The <see cref="World"/> in which the section is loaded.</param>
        /// <param name="sectionEntity">The section entity representing the scene section. The section entities can be found in the ResolvedSectionEntity buffer on the scene entity.</param>
        /// <returns>The streaming state of the loading section.</returns>
        public static SectionStreamingState GetSectionStreamingState(WorldUnmanaged world, Entity sectionEntity)
        {
            bool requestLoad = world.EntityManager.HasComponent<RequestSceneLoaded>(sectionEntity);
            if (world.EntityManager.HasComponent<SceneSectionStreamingSystem.StreamingState>(sectionEntity))
            {
                if (requestLoad)
                {
                    var internalStatus = world.EntityManager
                        .GetComponentData<SceneSectionStreamingSystem.StreamingState>(sectionEntity).Status;
                    switch (internalStatus)
                    {
                        case SceneSectionStreamingSystem.StreamingStatus.Loaded:
                            return SectionStreamingState.Loaded;
                        case SceneSectionStreamingSystem.StreamingStatus.NotYetProcessed:
                            return SectionStreamingState.LoadRequested;
                        case SceneSectionStreamingSystem.StreamingStatus.Loading:
                            return SectionStreamingState.Loading;
                        case SceneSectionStreamingSystem.StreamingStatus.FailedToLoad:
                            return SectionStreamingState.FailedToLoad;
                    }
                }
                else
                    // The Section is loaded or loading, but unloading has been requested by deleting the component RequestSceneLoaded
                    return SectionStreamingState.UnloadRequested;
            }
            else if (requestLoad)
            {
                // This could happen when the component RequestSceneLoaded has been added, but the systems haven't run yet.
                return SectionStreamingState.LoadRequested;
            }
            return SectionStreamingState.Unloaded;
        }

        /// <summary>
        /// Check if a section of a subscene is loaded.
        /// </summary>
        /// <param name="world">The <see cref="World"/> in which the section is loaded.</param>
        /// <param name="sectionEntity">The section entity representing the scene section. The section entities can be found in the ResolvedSectionEntity buffer on the scene entity.</param>
        /// <returns>True if the scene section is loaded.</returns>
        public static bool IsSectionLoaded(WorldUnmanaged world, Entity sectionEntity)
        {
            if (!world.EntityManager.HasComponent<SceneSectionStreamingSystem.StreamingState>(sectionEntity))
                return false;

            return (SceneSectionStreamingSystem.StreamingStatus.Loaded ==
                    world.EntityManager.GetComponentData<SceneSectionStreamingSystem.StreamingState>(sectionEntity).Status);
        }

        /// <summary>
        /// Load a scene by its weak reference id.
        /// </summary>
        /// <param name="world">The <see cref="World"/> in which the scene is loaded.</param>
        /// <param name="sceneReferenceId">The weak asset reference to the scene.</param>
        /// <param name="parameters">The load parameters for the scene.</param>
        /// <returns>An entity representing the loading state of the scene.</returns>
        public static Entity LoadSceneAsync(WorldUnmanaged world, EntitySceneReference sceneReferenceId, LoadParameters parameters = default)
        {
            return LoadSceneAsync(world, sceneReferenceId.Id .GlobalId.AssetGUID, parameters);
        }

        /// <summary>
        /// Load a prefab by its weak reference id.
        /// A PrefabRoot component is added to the returned entity when the load completes.
        /// </summary>
        /// <param name="world">The <see cref="World"/> in which the prefab is loaded.</param>
        /// <param name="prefabReferenceId">The weak asset reference to the prefab.</param>
        /// <param name="parameters">The load parameters for the prefab.</param>
        /// <returns>An entity representing the loading state of the prefab.</returns>
        public static Entity LoadPrefabAsync(WorldUnmanaged world, EntityPrefabReference prefabReferenceId, LoadParameters parameters = default)
        {
            return LoadSceneAsync(world, prefabReferenceId.Id.GlobalId.AssetGUID, parameters);
        }

        /// <summary>
        /// Load a scene or prefab by its asset GUID.
        /// When loading a prefab a PrefabRoot component is added to the scene entity when the load completes.
        /// </summary>
        /// <param name="world">The <see cref="World"/> in which the prefab is loaded.</param>
        /// <param name="sceneGUID">The guid of the scene or prefab.</param>
        /// <param name="parameters">The load parameters for the scene or prefab.</param>
        /// <returns>An entity representing the loading state of the scene or prefab.</returns>
        public static Entity LoadSceneAsync(WorldUnmanaged world, Hash128 sceneGUID, LoadParameters parameters = default)
        {
            if (!sceneGUID.IsValid)
            {
                Debug.LogError($"LoadSceneAsync - Invalid sceneGUID.");
                return Entity.Null;
            }

            return LoadEntitySceneAsync(world, sceneGUID, parameters);
        }

        private static Entity LoadEntitySceneAsync(WorldUnmanaged world, Hash128 sceneGUID, LoadParameters parameters)
        {
            var sceneEntity = Entity.Null;
            if ((parameters.Flags & SceneLoadFlags.NewInstance) == 0)
            {
                var builder = new EntityQueryBuilder(Allocator.Temp).WithAll<SceneReference>();
                using var q = world.EntityManager.CreateEntityQuery(builder);
                foreach (var e in q.ToEntityArray(Allocator.Temp))
                {
                    var r = world.EntityManager.GetComponentData<SceneReference>(e);
                    if (r.SceneGUID == sceneGUID)
                        sceneEntity = e;
                }
            }

            if (sceneEntity == Entity.Null)
                return CreateSceneEntity(world, sceneGUID, parameters);

            LoadEntitySceneAsync(world, sceneEntity, parameters);
            return sceneEntity;
        }

        static void LoadEntitySceneAsync(WorldUnmanaged world, Entity sceneEntity, LoadParameters parameters)
        {
            var requestSceneLoaded = CreateRequestSceneLoaded(parameters);

            world.EntityManager.AddComponentData(sceneEntity, requestSceneLoaded);

            if (parameters.AutoLoad && world.EntityManager.HasComponent<ResolvedSectionEntity>(sceneEntity))
            {
                var resolvedSectionEntities = world.EntityManager.GetBuffer<ResolvedSectionEntity>(sceneEntity).ToNativeArray(Allocator.Temp);
                for (int i = 0; i < resolvedSectionEntities.Length; i++)
                    world.EntityManager.AddComponentData(resolvedSectionEntities[i].SectionEntity, requestSceneLoaded);
            }
        }

        /// <summary>
        /// Loads a scene.
        /// </summary>
        /// <param name="world">The <see cref="World"/> in which the scene is loaded.</param>
        /// <param name="sceneEntity">The entity representing the loading state of the scene.</param>
        /// <param name="parameters">The load parameters for the scene or prefab.</param>
        public static void LoadSceneAsync(WorldUnmanaged world, Entity sceneEntity, LoadParameters parameters = default)
        {
            LoadEntitySceneAsync(world, sceneEntity, parameters);
        }

        static Entity CreateSceneEntity(WorldUnmanaged world, Hash128 sceneGUID, LoadParameters parameters = default)
        {
            var requestSceneLoaded = CreateRequestSceneLoaded(parameters);
            var arr = new NativeArray<ComponentType>(2, Allocator.Temp);
            arr[0] = ComponentType.ReadWrite<SceneReference>();
            arr[1] = ComponentType.ReadWrite<RequestSceneLoaded>();
            var sceneEntity = world.EntityManager.CreateEntity(world.EntityManager.CreateArchetype(arr));
            world.EntityManager.SetComponentData(sceneEntity, new SceneReference {SceneGUID = sceneGUID});
            world.EntityManager.SetComponentData(sceneEntity, requestSceneLoaded);
            return sceneEntity;
        }

        /// <summary>
        /// Flags controlling the unload process for SubScenes.
        /// </summary>
        [Flags]
        public enum UnloadParameters
        {
            /// <summary>
            /// Options for the default unloading behavior. Destroys the request scene loaded entity, but preserves the section and scene entities when the unload completes.
            /// </summary>
            Default = 0,
            /// <summary>
            /// In addition, it destroys the scene and sections meta entities when unloading the scene.
            /// </summary>
            DestroyMetaEntities = 1 << 1,
        }

        /// <summary>
        /// Unload the scene.
        /// </summary>
        /// <param name="world">The world from which to unload the scene.</param>
        /// <param name="sceneEntity">The entity for the scene.</param>
        /// <param name="unloadParams">Parameters controlling the unload process.</param>
        /// <remarks>
        /// By default this function will keep the scene and section meta entities alive and just unload the content for the sections. Keeping these meta entities alive will speed up any potential reloading of the scene.
        /// Call the function with unloadParams set to UnloadParameters.DestroyMetaEntities to destroy those meta entities and fully unload the scene.
        /// </remarks>
        public static void UnloadScene(WorldUnmanaged world, Entity sceneEntity, UnloadParameters unloadParams = UnloadParameters.Default)
        {
            bool destroySceneProxyEntity = (unloadParams & UnloadParameters.DestroyMetaEntities) != 0;
            if (world.EntityManager.HasComponent<ResolvedSectionEntity>(sceneEntity))
            {
                using (var sections = world.EntityManager.GetBuffer<ResolvedSectionEntity>(sceneEntity).ToNativeArray(Allocator.Temp))
                {
                    foreach (var section in sections)
                    {
                        SceneSectionStreamingSystem.UnloadSectionImmediate(world, section.SectionEntity);

                        if (destroySceneProxyEntity)
                            world.EntityManager.DestroyEntity(section.SectionEntity);
                        else
                            world.EntityManager.RemoveComponent<RequestSceneLoaded>(section.SectionEntity);
                    }
                }
            }

            if (destroySceneProxyEntity)
            {
                world.EntityManager.RemoveComponent<LinkedEntityGroup>(sceneEntity);
                world.EntityManager.DestroyEntity(sceneEntity);
            }
            else
            {
                world.EntityManager.RemoveComponent<RequestSceneLoaded>(sceneEntity);
            }
        }

        internal static void UnloadSceneSectionMetaEntitiesOnly(WorldUnmanaged world, Entity sceneEntity, bool removeRequestSceneLoaded)
        {
            // Exit if the scene entity is not valid, we added this check as RemoveComponent with a ComponentTypeSet and an invalid entity throws an error.
            if (!world.EntityManager.Exists(sceneEntity))
            {
                return;
            }

            if (world.EntityManager.HasComponent<ResolvedSectionEntity>(sceneEntity))
            {
                using (var sections = world.EntityManager.GetBuffer<ResolvedSectionEntity>(sceneEntity).ToNativeArray(Allocator.Temp))
                {
                    foreach (var section in sections)
                    {
                        SceneSectionStreamingSystem.UnloadSectionImmediate(world, section.SectionEntity);
                    }
                    world.EntityManager.DestroyEntity(sections.Reinterpret<Entity>());
                }
            }

            var removeComponentSet = new ComponentTypeSet(
                ComponentType.ReadOnly<ResolvedSectionEntity>(),
                ComponentType.ReadOnly<LinkedEntityGroup>(),
                ComponentType.ReadOnly<ResolvedSceneHash>());

            world.EntityManager.RemoveComponent(sceneEntity, removeComponentSet);

            if (removeRequestSceneLoaded)
                world.EntityManager.RemoveComponent<RequestSceneLoaded>(sceneEntity);
        }

        /// <summary>
        /// Unload a SubScene by its weak reference id. This will only unload the first matching scene.
        /// </summary>
        /// <param name="world">The <see cref="World"/> in which the scene is loaded.</param>
        /// <param name="sceneReferenceId">The weak asset reference to the scene.</param>
        /// <param name="unloadParams">Parameters controlling the unload process.</param>
        /// <remarks>
        /// By default this function will keep the scene and section meta entities alive and just unload the content for the sections. Keeping these meta entities alive will speed up any potential reloading of the scene.
        /// Call the function with unloadParams set to UnloadParameters.DestroyMetaEntities to destroy those meta entities and fully unload the scene.
        ///
        /// The version of this function receiving an entity scene instead of an EntityPrefabReference is faster because no lookup is needed.
        /// </remarks>
        public static void UnloadScene(WorldUnmanaged world, EntityPrefabReference sceneReferenceId, UnloadParameters unloadParams = UnloadParameters.Default)
        {
            UnloadScene(world, sceneReferenceId.Id.GlobalId.AssetGUID, unloadParams);
        }

        /// <summary>
        /// Unload a SubScene by GUID.  This will only unload the first matching scene.
        /// </summary>
        /// <param name="world">The <see cref="World"/> in which the scene is loaded.</param>
        /// <param name="sceneGUID">The guid of the scene.</param>
        /// <param name="unloadParams">Parameters controlling the unload process.</param>
        /// <remarks>
        /// By default this function will keep the scene and section meta entities alive and just unload the content for the sections.  Keeping these meta entities alive will speed up any potential reloading of the scene.
        /// Call the function with unloadParams set to UnloadParameters.DestroyMetaEntities to destroy those meta entities and fully unload the scene.
        ///
        /// The version of this function receiving an entity scene instead of sceneGUID is faster because no lookup is needed.
        /// </remarks>
        public static void UnloadScene(
            WorldUnmanaged world,
            Hash128 sceneGUID,
            UnloadParameters unloadParams = UnloadParameters.Default)
        {
            var sceneEntity = GetSceneEntity(world, sceneGUID);
            if (sceneEntity != Entity.Null)
                UnloadScene(world, sceneEntity, unloadParams);
        }

        /// <summary>
        /// Find the scene given a guid.  This will only return the first matching scene.
        /// </summary>
        /// <param name="world">The <see cref="World"/> in which the scene is loaded.</param>
        /// <param name="sceneGUID">The guid of the scene.</param>
        /// <returns>The entity for the scene.</returns>su
        public static Entity GetSceneEntity(WorldUnmanaged world, Hash128 sceneGUID)
        {
            Entity sceneEntity = Entity.Null;

            using var q = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SceneReference>());
            foreach (var e in q.ToEntityArray(Allocator.Temp))
            {
                var r = world.EntityManager.GetComponentData<SceneReference>(e);
                if (r.SceneGUID == sceneGUID)
                    sceneEntity = e;
            }

            return sceneEntity;
        }

        /// <summary>
        /// Find the scene given a guid and no DisableLiveConversion component.  This will only return the first matching scene.
        /// </summary>
        /// <param name="state">The entity system state.</param>
        /// <param name="sceneGUID">The guid of the scene.</param>
        /// <returns>The entity for the scene.</returns>
        internal static Entity GetLiveConvertedSceneEntity(ref SystemState state, Hash128 sceneGUID)
        {
            Entity sceneEntity = Entity.Null;

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SceneReference>()
                .WithNone<DisableLiveConversion>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
                .Build(ref state);
            foreach (var e in query.ToEntityArray(Allocator.Temp))
            {
                var r = state.EntityManager.GetComponentData<SceneReference>(e);
                if (r.SceneGUID == sceneGUID)
                    sceneEntity = e;
            }

            return sceneEntity;
        }

        /// <summary>
        /// Callback invoked when the system is updated.
        /// </summary>
        /// <param name="state">The entity system state.</param>
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Cleanup all Scenes that were destroyed explicitly
            if (!_unloadSceneQuery.IsEmptyIgnoreFilter)
            {
                foreach (var e in _unloadSceneQuery.ToEntityArray(Allocator.Temp))
                {
                    SceneSectionStreamingSystem.UnloadSectionImmediate(state.WorldUnmanaged, e);
                }
            }
        }

    }
}
