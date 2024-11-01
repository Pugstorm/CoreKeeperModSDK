using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;
using Unity.Entities.Serialization;
using System.Runtime.InteropServices;

namespace Unity.Scenes
{
    /// <summary>
    /// The group of systems that runs after a scene is loaded
    /// This allows for custom post processing of loaded SubScenes
    /// ie scene offsetting
    /// </summary>
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.ProcessAfterLoad)]
    public partial class ProcessAfterLoadGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// The tag component is added to scene entities when they are loaded.
    /// </summary>
    public struct IsSectionLoaded : IComponentData
    {}

    internal struct SceneSectionStreamingData : IComponentData
    {
        internal EntityQuery m_NestedScenes;
        internal EntityQuery m_SceneFilter;
    }

    /// <summary>
    /// System that controls streaming scene sections.
    /// </summary>
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor | WorldSystemFilterFlags.Streaming)]
    [UpdateInGroup(typeof(SceneSystemGroup))]
    [UpdateAfter(typeof(ResolveSceneReferenceSystem))]
    [BurstCompile]
    public partial class SceneSectionStreamingSystem : SystemBase
    {
        internal enum StreamingStatus
        {
            NotYetProcessed,
            Loaded,
            Loading,
            FailedToLoad
        }

        internal struct StreamingState : ICleanupComponentData
        {
            public StreamingStatus Status;
            public int            ActiveStreamIndex;
        }

        struct Stream
        {
            public World                   World;
            public Entity                  SectionEntity;
            public AsyncLoadSceneOperation Operation;
        }

        int m_MaximumWorldsMovedPerUpdate = 1;
        int m_MaximumSectionsUnloadedPerUpdate = -1;

        World m_SynchronousSceneLoadWorld;

        const int k_InitialConcurrentSectionStreamCount = 4;
        int m_ConcurrentSectionStreamCount;
        Stream[] m_Streams = Array.Empty<Stream>();

        EntityQuery m_PendingStreamRequests;
        EntityQuery m_UnloadStreamRequests;
        EntityQuery m_NestedScenesPending;
        EntityQuery m_PublicRefFilter;
        EntityQuery m_SectionData;

        readonly ProfilerMarker s_MoveEntitiesFrom = new ProfilerMarker("SceneStreaming.MoveEntitiesFrom");
        readonly ProfilerMarker s_ExtractEntityRemapRefs = new ProfilerMarker("SceneStreaming.ExtractEntityRemapRefs");
        readonly ProfilerMarker s_AddSceneSharedComponents = new ProfilerMarker("SceneStreaming.AddSceneSharedComponents");
        static readonly ProfilerMarker s_UnloadSectionImmediate = new ProfilerMarker("SceneStreaming." + nameof(UnloadSectionImmediate));
        readonly ProfilerMarker s_MoveEntities = new ProfilerMarker("SceneStreaming." + nameof(MoveEntities));
        readonly ProfilerMarker s_UpdateLoadOperation = new ProfilerMarker("SceneStreaming." + nameof(UpdateLoadOperation));

        /// <summary>
        /// The maximum amount of sections that will be streamed in concurrently.
        /// This defaults to 4.
        /// </summary>
        public int ConcurrentSectionStreamCount
        {
            get => m_ConcurrentSectionStreamCount;
            set
            {
                if (value > m_Streams.Length)
                {
                    Array.Resize(ref m_Streams, value);
                    for (int i = m_ConcurrentSectionStreamCount; i < value; ++i)
                    {
                        if (m_Streams[i].World == null)
                            CreateStreamWorld(i);
                    }
                }
                else
                {
                    for (int i = value; i < m_Streams.Length; ++i)
                    {
                        if ((m_Streams[i].Operation == null) && (m_Streams[i].World != null))
                            DestroyStreamWorld(i);
                    }
                }
                m_ConcurrentSectionStreamCount = value;
            }
        }

        /// <summary>
        /// The maximum amount of streaming worlds that will be moved into the main world per update.
        /// This defaults to 1.
        /// </summary>
        public int MaximumWorldsMovedPerUpdate
        {
            get => m_MaximumWorldsMovedPerUpdate;
            set => m_MaximumWorldsMovedPerUpdate = value;
        }

        /// <summary>
        /// The maximum number of scene sections that will be unloaded per update tick. A value equal to or below zero
        /// indicates that the number of sections to unload per update is unlimited.
        /// This defaults to a negative value.
        /// </summary>
        public int MaximumSectionsUnloadedPerUpdate
        {
            get => m_MaximumSectionsUnloadedPerUpdate;
            set => m_MaximumSectionsUnloadedPerUpdate = value;
        }

        // Exposed for testing
        internal int StreamArrayLength => m_Streams.Length;
        internal bool AllStreamsComplete
        {
            get
            {
                for (int i = 0; i < m_Streams.Length; ++i) {
                    if (m_Streams[i].Operation != null && !m_Streams[i].Operation.IsCompleted)
                        return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Called when this system is created.
        /// </summary>
        protected override void OnCreate()
        {
            using var marker = new ProfilerMarker("SceneSectionStreamingSystem.OnCreate").Auto();

            ConcurrentSectionStreamCount = k_InitialConcurrentSectionStreamCount;

            m_SynchronousSceneLoadWorld = new World("LoadingWorld (synchronous)", WorldFlags.Streaming);

            m_PendingStreamRequests = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<RequestSceneLoaded, SceneSectionData>()
                .WithAllRW<ResolvedSectionPath>()
                .WithNone<StreamingState, DisableSceneResolveAndLoad>()
                .Build(this);
            m_UnloadStreamRequests = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<StreamingState>()
                .WithAll<SceneSectionData,SceneEntityReference>()
                .WithNone<RequestSceneLoaded, DisableSceneResolveAndLoad>()
                .Build(this);
            m_NestedScenesPending = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<RequestSceneLoaded, SceneTag>()
                .WithNone<StreamingState, DisableSceneResolveAndLoad>()
                .Build(this);

            EntityManager.AddComponentData(SystemHandle, new SceneSectionStreamingData
            {
                m_NestedScenes = new EntityQueryBuilder(Allocator.Temp)
                    .WithAllRW<RequestSceneLoaded,SceneTag>()
                    .Build(this),
                m_SceneFilter = new EntityQueryBuilder(Allocator.Temp)
                    .WithAllRW<SceneTag>()
                    .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
                    .Build(this),
            });

            m_PublicRefFilter = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<SceneTag, PublicEntityRef>()
                .Build(this);
            m_SectionData = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<SceneSectionData,SceneEntityReference>()
                .Build(this);
        }

        /// <summary>
        /// Called when this system is destroyed.
        /// </summary>
        protected override void OnDestroy()
        {
            for (int i = 0; i != m_Streams.Length; i++)
            {
                m_Streams[i].Operation?.Dispose();
                if(m_Streams[i].World != null)
                    DestroyStreamWorld(i);
            }

            m_SynchronousSceneLoadWorld.Dispose();
        }

        void DestroyStreamWorld(int index)
        {
            m_Streams[index].World.Dispose();
            m_Streams[index].World = null;
            m_Streams[index].Operation = null;
        }

        void CreateStreamWorld(int index)
        {
            m_Streams[index].World = new World("LoadingWorld" + index, WorldFlags.Streaming);
        }

        struct ProcessAfterLoadRootGroups : DefaultWorldInitialization.IIdentifyRootGroups
        {
            public bool IsRootGroup(SystemTypeIndex type) =>
                type == TypeManager.GetSystemTypeIndex<ProcessAfterLoadGroup>();
        }

        static internal void AddStreamingWorldSystems(World world)
        {
            using var marker = new ProfilerMarker("AddSystems").Auto();

            var group = world.GetOrCreateSystemManaged<ProcessAfterLoadGroup>();
            DefaultWorldInitialization.AddSystemToRootLevelSystemGroupsInternal(world,
                DefaultWorldInitialization.GetAllSystemTypeIndices(WorldSystemFilterFlags.ProcessAfterLoad),
                group,
                new ProcessAfterLoadRootGroups());
            group.SortSystems();
        }

        static SceneTag SceneTagForSection0(Entity sceneEntity, EntityQuery sectionDataQuery)
        {
            using var sectionDataEntities = sectionDataQuery.ToEntityArray(Allocator.Temp);
            using var sectionData = sectionDataQuery.ToComponentDataArray<SceneSectionData>(Allocator.Temp);
            using var sceneEntityReference = sectionDataQuery.ToComponentDataArray<SceneEntityReference>(Allocator.Temp);

            for (int i = 0; i < sectionData.Length; ++i)
            {
                // Multiple instances of the same scene can co-exist, they need to be differentiated.
                // Searching for the scene GUID instead of the scene entity would be ambiguous.
                if (
                    sceneEntity == sceneEntityReference[i].SceneEntity &&
                    sectionData[i].SubSectionIndex == 0
                )
                {
                    return new SceneTag { SceneEntity = sectionDataEntities[i] };
                }
            }

            throw new InvalidOperationException("Could not find the scene entity corresponding to the section being loaded!");
        }

        struct EntityRemapArgs
        {
            public EntityManager EntityManager;
            public EntityManager SrcManager;
            public Entity SceneEntity;
            public EntityQuery SectionData;
            public EntityQuery PublicRefFilter;
            public SceneSectionData SceneSectionData;

            public NativeArray<EntityRemapUtility.EntityRemapInfo> OutEntityRemapping;
        }

        bool MoveEntities(EntityManager srcManager, Entity sectionEntity, ref Entity prefabRoot)
        {
            using var marker = s_MoveEntities.Auto();
            var sceneEntity = SystemAPI.GetComponent<SceneEntityReference>(sectionEntity).SceneEntity;
            Assert.AreNotEqual(Entity.Null, sceneEntity);

            NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapping;
            using (s_ExtractEntityRemapRefs.Auto())
            {
                var sceneSectionData = SystemAPI.GetComponent<SceneSectionData>(sectionEntity);
                var remapArgs = new EntityRemapArgs
                {
                    EntityManager = EntityManager,
                    SectionData = m_SectionData,
                    PublicRefFilter = m_PublicRefFilter,
                    SrcManager = srcManager,
                    SceneEntity = sceneEntity,
                    SceneSectionData = sceneSectionData
                };
                if (!ExtractEntityRemapRefs(ref remapArgs))
                    return false;
                entityRemapping = remapArgs.OutEntityRemapping;
            }

#if UNITY_EDITOR
            {
#if ENTITY_STORE_V1
                var startCapacity = srcManager.EntityCapacity;
#endif
                using (s_AddSceneSharedComponents.Auto())
                {
                    var data = new EditorRenderData
                    {
                        SceneCullingMask = UnityEditor.SceneManagement.EditorSceneManager.DefaultSceneCullingMask | (1UL << 59)
                    };
                    srcManager.AddSharedComponentManaged(srcManager.UniversalQuery, data);
                }

#if ENTITY_STORE_V1
                var endCapacity = srcManager.EntityCapacity;

                // ExtractEntityRemapRefs gathers entityRemapping based on Entities Capacity.
                // MoveEntitiesFrom below assumes that AddSharedComponentData on srcManager.UniversalQuery does not affect capacity.
                Assert.AreEqual(startCapacity, endCapacity);
#endif
            }
#endif

            using (s_MoveEntitiesFrom.Auto())
            {
                using (var builder = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<SystemInstance>())
                {
                    using (var query = srcManager.CreateEntityQuery(builder))
                    {
                        if (query.CalculateEntityCount() != 0)
                        {
                            throw new InvalidOperationException("System entities can not exist in the streaming world");
                        }
                    }

                    EntityManager.MoveEntitiesFrom(srcManager, entityRemapping);
                }
            }

            if (prefabRoot != Entity.Null)
                prefabRoot = EntityRemapUtility.RemapEntity(ref entityRemapping, prefabRoot);

            entityRemapping.Dispose();
            srcManager.PrepareForDeserialize();

            return true;
        }

        // The Burst compilation of the following function causes ECSB-621
        // It can be re-enabled once UUM-46084 (fix for the root cause) has landed
        // [BurstCompile]
        static bool ExtractEntityRemapRefs(ref EntityRemapArgs args)
        {
#if !ENTITY_STORE_V1
            int remapTableSize = args.SrcManager.HighestEntityIndex() + 1;
#else
            int remapTableSize = args.SrcManager.EntityCapacity;
#endif

            if (args.SceneSectionData.SubSectionIndex == 0)
            {
                // External entity references are "virtual" entities. If we don't have any, only real entities need remapping
                // Section 0 doesn't have an external ref info, let's skip the whole process.
                args.OutEntityRemapping = new NativeArray<EntityRemapUtility.EntityRemapInfo>(remapTableSize, Allocator.TempJob);
                return true;
            }

#if !ENTITY_STORE_V1
            NativeArray<Entity> externalReferences;

            unsafe
            {
                // The singleton buffer is added when the chunks are deserialized, it contains all the placeholder entities
                // which have been created for the external references. Those entities are not stored in any chunk at this
                // point. Every section beyond section 0 should have this buffer added.
                var type = ComponentType.ReadOnly<ExternalEntityReference>();
                using (var extRefQuery = args.SrcManager.CreateEntityQuery(&type, 1))
                {
                    if (!extRefQuery.TryGetSingletonBuffer<ExternalEntityReference>(out var buffer, true))
                    {
                        // If the array is missing, the scene section 0 isn't loaded, we have to wait.
                        return false;
                    }
                    externalReferences = buffer.AsNativeArray().Reinterpret<Entity>();
                }
            }

            // The remapping table needs to be as big as the largest entity index that can be encountered in the chunks.
            // The real entities are already accounted for, but the placeholder entities for the external references aren't.
            // There's no other way than to figure out which one has the highest index. Remapping shouldn't use arrays anymore.
            for (int ei = 0, ec = externalReferences.Length; ei < ec; ei++)
            {
                var idx = externalReferences[ei].Index;
                if (idx >= remapTableSize)
                {
                    remapTableSize = idx + 1;
                }
            }
#else
            // Loading a section which isn't section 0. We need to figure out the exact count of entities in the section,
            // because immediately after that comes the external references that need remapping.
            // Above we could use EntityCapacity as an approximation, it's faster than going over the archetypes to get the exact count.
            int entitiesCount = 0;
            // External entity references are "virtual" entities. If we don't have any, only real entities need remapping

            unsafe
            {
                var access = args.SrcManager.GetCheckedEntityDataAccess();
                var archetypes = access->EntityComponentStore->m_Archetypes;
                for (int ai = 0, ac = archetypes.Length; ai < ac; ai++)
                {
                    entitiesCount += archetypes[ai]->EntityCount;
                }
            }

            remapTableSize = entitiesCount;
#endif

            // Within a scene, external scenes are identified by some ID
            // In the destination world, scenes are identified by an entity
            // Every entity coming from a scene needs to have a SceneTag that references the scene entity
            var sceneTag = SceneTagForSection0(args.SceneEntity, args.SectionData);

            // A scene section 0 is expected to have a single public reference array
            args.PublicRefFilter.SetSharedComponentFilter(sceneTag);
            var pubRefEntities = args.PublicRefFilter.ToEntityArray(Allocator.Temp);

            if (pubRefEntities.Length == 0)
            {
                // If the array is missing, the scene section 0 isn't loaded, we have to wait.
                args.OutEntityRemapping.Dispose();
                return false;
            }

            if (pubRefEntities.Length > 1)
            {
                throw new InvalidOperationException("Multiple entities containing public references arrays found in scene section 0!");
            }

            var pubRefs = args.EntityManager.GetBuffer<PublicEntityRef>(pubRefEntities[0]);

#if ENTITY_STORE_V1
            // Space is required to handle every possible external reference that needs remapping.
            remapTableSize += pubRefs.Length;
#endif

            args.OutEntityRemapping = new NativeArray<EntityRemapUtility.EntityRemapInfo>(remapTableSize, Allocator.TempJob);

            // Proper mapping from external reference in section to entity in main world
            for (int k = 0; k < pubRefs.Length; ++k)
            {
#if !ENTITY_STORE_V1
                // Note that we come from version 0, which isn't the actual version of the placeholder entity.
                // But the chunks have been remapped to 0 in order for the entities to be considered invalid
                // even though they are properly allocated. This is important to ensure some consistency
                // of the streaming world in case a post-load system has to run there.
                var source = new Entity { Index = externalReferences[k].Index, Version = 0 };
#else
                var source = new Entity{ Index = entitiesCount + k, Version = 1 };
#endif
                var target = pubRefs[k].targetEntity;

                args.OutEntityRemapping[source.Index] = new EntityRemapUtility.EntityRemapInfo
                {
                    SourceVersion = source.Version,
                    Target = target
                };
            }

            return true;
        }

        bool ProcessActiveStreams(ref SystemState state)
        {
            bool needsMoreProcessing = false;
            int moveEntitiesFromProcessed = 0;

            int lastOperationIndex = -1;

            for (int i = 0; i != m_Streams.Length; i++)
            {
                if (m_Streams[i].Operation != null)
                {
                    needsMoreProcessing = true;

                    bool moveEntities = moveEntitiesFromProcessed < m_MaximumWorldsMovedPerUpdate;
                    switch (UpdateLoadOperation(ref state, m_Streams[i].Operation, m_Streams[i].World, m_Streams[i].SectionEntity, moveEntities))
                    {
                        case UpdateLoadOperationResult.Completed:
                        {
                            moveEntitiesFromProcessed += 1;
                            m_Streams[i].Operation.Dispose();
                            m_Streams[i].Operation = null;
                            break;
                        }
                        case UpdateLoadOperationResult.Aborted:
                        {
                            m_Streams[i].Operation.Dispose();
                            m_Streams[i].Operation = null;
                            break;
                        }
                        case UpdateLoadOperationResult.Error:
                        {
                            DestroyStreamWorld(i);
                            if (i < m_ConcurrentSectionStreamCount)
                                CreateStreamWorld(i);
                            break;
                        }
                        case UpdateLoadOperationResult.Busy:
                        {
                            lastOperationIndex = i;
                            // carry on
                            break;
                        }
                        default:
                        {
                            throw new ArgumentOutOfRangeException();
                        }
                    }
                }
            }

            if((m_Streams.Length > m_ConcurrentSectionStreamCount) && (lastOperationIndex < m_ConcurrentSectionStreamCount))
            {
                for (int i = m_ConcurrentSectionStreamCount; i < m_Streams.Length; ++i)
                {
                    m_Streams[i].World?.Dispose();
                }
                Array.Resize(ref m_Streams, m_ConcurrentSectionStreamCount);
            }

            return needsMoreProcessing;
        }

        enum UpdateLoadOperationResult
        {
            Busy,
            Aborted,
            Completed,
            Error,
        }

        unsafe UpdateLoadOperationResult UpdateLoadOperation(
            ref SystemState systemState,
            AsyncLoadSceneOperation operation,
            World streamingWorld,
            Entity sectionEntity,
            bool moveEntities)
        {
            using var marker = s_UpdateLoadOperation.Auto();
            operation.Update();

            var streamingManager = streamingWorld.EntityManager;

            if (operation.IsCompleted)
            {
                try
                {
                    // Loading failed, EntityManager is in unknown state. Just wipe it out and create a clean one.
                    if (operation.ErrorStatus == null)
                    {
                        streamingManager.EndExclusiveEntityTransaction();

                        Entity prefabRoot = operation.DeserializationResult.PrefabRoot;

                        if (EntityManager.HasComponent<RequestSceneLoaded>(sectionEntity))
                        {
                            if(!moveEntities)
                                return UpdateLoadOperationResult.Busy;

                            if (MoveEntities(streamingManager, sectionEntity, ref prefabRoot))
                            {

                                if (EntityManager.HasComponent<StreamingState>(sectionEntity))
                                {
                                    var state = EntityManager.GetComponentData<StreamingState>(sectionEntity);
                                    state.Status = StreamingStatus.Loaded;
                                    EntityManager.SetComponentData(sectionEntity, state);
                                    EntityManager.AddComponentData(sectionEntity, default(IsSectionLoaded));
                                }

                                var objRefs = operation.StealReferencedUnityObjects();
                                if (objRefs.IsValid)
                                {
                                    EntityManager.AddSharedComponentManaged(sectionEntity, new SceneSectionReferencedUnityObjects(objRefs));
                                    RuntimeContentManager.ReleaseObjectAsync(objRefs);
                                }

                                if (prefabRoot != Entity.Null)
                                {
                                    var sceneEntity = EntityManager.GetComponentData<SceneEntityReference>(sectionEntity).SceneEntity;
                                    EntityManager.AddComponentData(sceneEntity, new PrefabRoot {Root = prefabRoot});
                                    if (EntityManager.HasComponent<WeakAssetPrefabLoadRequest>(sceneEntity))
                                    {
                                        WeakAssetReferenceLoadingSystem.CompleteLoad(ref systemState,
                                            sceneEntity,
                                            prefabRoot,
                                            EntityManager.GetComponentData<WeakAssetPrefabLoadRequest>(sceneEntity)
                                                .WeakReferenceId);
                                    }
                                }

                                // if this section was loaded with block on import, propagate those flags to immediate children
                                RequestSceneLoaded parentRequestSceneLoaded = EntityManager.GetComponentData<RequestSceneLoaded>(sectionEntity);
                                if ((parentRequestSceneLoaded.LoadFlags & (SceneLoadFlags.BlockOnImport|SceneLoadFlags.BlockOnStreamIn)) != 0)
                                {
                                    m_NestedScenesPending.SetSharedComponentFilter(new SceneTag { SceneEntity = sectionEntity });
                                    using (NativeArray<Entity> entities = m_NestedScenesPending.ToEntityArray(Allocator.Temp))
                                    {
                                        for (int i = 0; i < entities.Length; ++i)
                                        {
                                            Entity entity = entities[i];
                                            RequestSceneLoaded requestSceneLoaded = EntityManager.GetComponentData<RequestSceneLoaded>(entity);
                                            requestSceneLoaded.LoadFlags |= parentRequestSceneLoaded.LoadFlags & (SceneLoadFlags.BlockOnImport|SceneLoadFlags.BlockOnStreamIn);
                                            EntityManager.SetComponentData<RequestSceneLoaded>(entity, requestSceneLoaded);
                                        }
                                    }
                                }

                                return UpdateLoadOperationResult.Completed;
                            }
                            else
                            {
                                // Debug.Log("MoveEntities on hold, waiting for main section");
                            }
                        }
                        // The SubScene is no longer being requested for load
                        else
                        {
                            streamingManager.DestroyEntity(streamingManager.UniversalQuery);
                            // RetainBlobAssets is a cleanup component and must thus be explicitly removed.
                            // Blob assets have at this point not yet been increased with refcount, so no leak should occurr
                            streamingManager.RemoveComponent<RetainBlobAssets>(streamingManager.UniversalQuery);
                            streamingManager.PrepareForDeserialize();

                            EntityManager.RemoveComponent<IsSectionLoaded>(sectionEntity);
                            // Do this last just in case there are exceptions.
                            // So that SetLoadFailureOnEntity can set the state to failure
                            if (EntityManager.HasComponent<StreamingState>(sectionEntity))
                                EntityManager.RemoveComponent<StreamingState>(sectionEntity);

                            return UpdateLoadOperationResult.Aborted;
                        }
                    }
                    else
                    {
                        if (operation.Exception != null)
                            Debug.LogException(operation.Exception);
                        Debug.LogWarning($"Error when processing '{operation}': {operation.ErrorStatus}");
                        SetLoadFailureOnEntity(sectionEntity);

                        return UpdateLoadOperationResult.Error;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Error when loading '{operation}': {e}");
                    Debug.LogException(e);
                    SetLoadFailureOnEntity(sectionEntity);

                    return UpdateLoadOperationResult.Error;
                }
            }

            return UpdateLoadOperationResult.Busy;
        }

        void SetLoadFailureOnEntity(Entity sceneEntity)
        {
            // If load fails, don't try to load the requestScene again.
            if (EntityManager.HasComponent<StreamingState>(sceneEntity))
            {
                var state = EntityManager.GetComponentData<StreamingState>(sceneEntity);
                state.Status = StreamingStatus.FailedToLoad;
                EntityManager.SetComponentData(sceneEntity, state);
                EntityManager.RemoveComponent<IsSectionLoaded>(sceneEntity);
            }
        }

        bool SceneSectionRequiresSynchronousLoading(Entity entity) =>
            (EntityManager.GetComponentData<RequestSceneLoaded>(entity).LoadFlags & SceneLoadFlags.BlockOnStreamIn) != 0;

        /// <summary>
        /// Called when this system is updated.
        /// </summary>
        protected override unsafe void OnUpdate()
        {
            // Sections > 0 need the external references from sections 0 and will wait for it to be loaded.
            // So we have to ensure sections 0 are loaded first, otherwise there's a risk of starving loading streams.
            if (!m_PendingStreamRequests.IsEmptyIgnoreFilter) {
                using (var entities = m_PendingStreamRequests.ToEntityArray(Allocator.TempJob))
                {
                    var priorityList = new NativeList<Entity>(Allocator.Temp);
                    var priorities = new NativeArray<int>(entities.Length, Allocator.Temp);
                    var sceneDataFromEntity = GetComponentLookup<SceneSectionData>(true);
                    var sceneEntityFromSection = GetComponentLookup<SceneEntityReference>(true);

                    // We need to make sure sections 0 load first within each group (sync and async)
                    for (int i = 0; i < entities.Length; ++i)
                    {
                        var entity = entities[i];
                        if (SceneSectionRequiresSynchronousLoading(entity))
                            priorities[i] = sceneDataFromEntity[entity].SubSectionIndex == 0 ? 0 : 1;
                        else
                            priorities[i] = sceneDataFromEntity[entity].SubSectionIndex == 0 ? 2 : 3;
                    }

                    // Load sections synchronously (Priorities 0 and 1)
                    for (int priority = 0; priority <= 1; ++priority)
                    {
                        for (int i = 0; i < entities.Length; ++i)
                        {
                            if (priorities[i] == priority)
                            {
                                var entity = entities[i];

                                // If we are not loading a section 0, we need to make sure that the section 0 is loaded.
                                // Otherwise the loading will get blocked.
                                if (priority == 0 || IsSection0Loaded(sceneEntityFromSection[entity].SceneEntity))
                                {
                                    LoadSectionSynchronously(entity, ref *m_StatePtr);
                                    sceneDataFromEntity.Update(this);
                                    sceneEntityFromSection.Update(this);
                                }
                                else
                                {
                                    // Throw error
                                    UnityEngine.Debug.LogError($"Can't load section {sceneDataFromEntity[entity].SubSectionIndex} synchronously because section 0 hasn't been loaded first. Loading section asynchronously instead");

                                    // Set the priority to 3 as it will load asynchronously and it is not a section 0
                                    priorities[i] = 3;
                                }
                            }
                        }
                    }

                    // Load sections asynchronously (Priorities 2 and 3)
                    for (int priority = 2; priority <= 3; ++priority)
                    {
                        for (int i = 0; i < entities.Length; ++i)
                        {
                            if (priorityList.Length == m_ConcurrentSectionStreamCount)
                            {
                                break;
                            }

                            if (priorities[i] == priority)
                                priorityList.Add(entities[i]);
                        }
                    }

                    var priorityArray = priorityList.AsArray();

                    foreach (var entity in priorityArray)
                    {
                        var streamIndex = CreateAsyncLoadScene(entity, false);
                        if (streamIndex != -1)
                        {
                            var streamingState = new StreamingState
                                {ActiveStreamIndex = streamIndex, Status = StreamingStatus.NotYetProcessed};
                            EntityManager.AddComponentData(entity, streamingState);
                        }
                    }
                }
            }

            if (!m_UnloadStreamRequests.IsEmptyIgnoreFilter)
            {
                var destroySubScenes = m_UnloadStreamRequests.ToEntityArray(Allocator.Temp);
                var sceneSectionDatas = m_UnloadStreamRequests.ToComponentDataArray<SceneSectionData>(Allocator.Temp);
                var sceneEntityReferences = m_UnloadStreamRequests.ToComponentDataArray<SceneEntityReference>(Allocator.Temp);

                var destroySubScenesLength = destroySubScenes.Length;
                var maxCount = destroySubScenesLength;
                if (maxCount > m_MaximumSectionsUnloadedPerUpdate && m_MaximumSectionsUnloadedPerUpdate > 0)
                    maxCount = m_MaximumSectionsUnloadedPerUpdate;
                int currentCount = 0;

                // We need to do 2 passes in case we set m_MaximumSectionsUnloadedPerUpdate > 0.
                // m_MaximumSectionsUnloadedPerUpdate > 0 means that not all the sections are going to be unloaded in
                // this frame. So we even if other sections are planned to unload, we can't unload section 0 first as
                // those sections might be marked for loading again in the next frame. Leaving the scene with section 0
                // unloaded and other sections loaded.

                // First pass for sections != 0
                for (int index = 0; index < destroySubScenesLength && currentCount < maxCount; ++index)
                {
                    if (sceneSectionDatas[index].SubSectionIndex != 0)
                    {
                        UnloadSectionImmediate(World.Unmanaged, destroySubScenes[index]);
                        ++currentCount;
                    }
                }

                // Check if there is any section 0 to be unloaded
                if (currentCount < maxCount)
                {
                    // Second pass for sections == 0
                    for (int index = 0; index < destroySubScenesLength && currentCount < maxCount; ++index)
                    {
                        if (sceneSectionDatas[index].SubSectionIndex == 0)
                        {
                            // Check that other sections are not loaded or loading
                            if (!CheckDependantSectionsLoaded(EntityManager, sceneEntityReferences[index].SceneEntity))
                            {
                                UnloadSectionImmediate(World.Unmanaged, destroySubScenes[index]);
                            }
                            else
                            {
                                // Trying to unload section 0, but another section in the same scene is still loaded
                                // Skipping this until the other sections are unloaded.
                                // SceneSystem.GetSectionStreamingState will return SectionStreamingState.UnloadRequested,
                                // until the other sections are unloaded or section 0 is requested to load again.
                            }
                            ++currentCount;
                        }
                    }
                }
            }

            if (ProcessActiveStreams(ref *m_StatePtr))
                EditorUpdateUtility.EditModeQueuePlayerLoopUpdate();
        }

        internal bool IsSection0Loaded(Entity sceneEntity)
        {
            if (EntityManager.HasBuffer<ResolvedSectionEntity>(sceneEntity))
            {
                var sectionEntities = EntityManager.GetBuffer<ResolvedSectionEntity>(sceneEntity);
                return SceneSystem.IsSectionLoaded(EntityManager.WorldUnmanaged, sectionEntities[0].SectionEntity);
            }
            return false;
        }

        internal void LoadSectionSynchronously(Entity entity, ref SystemState systemState)
        {
            var streamingState = new StreamingState
                {ActiveStreamIndex = -1, Status = StreamingStatus.NotYetProcessed};
            EntityManager.AddComponentData(entity, streamingState);

            var operation = CreateAsyncLoadSceneOperation(m_SynchronousSceneLoadWorld.EntityManager,
                entity, true);
            var result = UpdateLoadOperation(ref systemState, operation, m_SynchronousSceneLoadWorld, entity, true);
            operation.Dispose();

            if (result == UpdateLoadOperationResult.Error)
            {
                m_SynchronousSceneLoadWorld.Dispose();
                m_SynchronousSceneLoadWorld =
                    new World("LoadingWorld (synchronous)", WorldFlags.Streaming);
            }
            Assert.AreNotEqual(UpdateLoadOperationResult.Aborted, result);
        }

        internal static bool CheckDependantSectionsLoaded(EntityManager entityManager, Entity sceneEntity)
        {
            var sectionEntities = entityManager.GetBuffer<ResolvedSectionEntity>(sceneEntity);
            for (int sectionIndex = 1; sectionIndex < sectionEntities.Length; ++sectionIndex)
            {
                var sectionEntity = sectionEntities[sectionIndex].SectionEntity;
                if (entityManager.HasComponent<RequestSceneLoaded>(sectionEntity))
                {
                    return true;
                }
            }
            return false;
        }

        internal static void UnloadSectionImmediate(WorldUnmanaged world, Entity scene)
        {
            using var marker = s_UnloadSectionImmediate.Auto();
            ref var singleton =
                ref world.GetExistingSystemState<SceneSectionStreamingSystem>().GetSingletonEntityQueryInternal(
                    ComponentType.ReadWrite<SceneSectionStreamingData>())
                .GetSingletonRW<SceneSectionStreamingData>().ValueRW;

            if (world.EntityManager.HasComponent<StreamingState>(scene))
            {
                // unload nested scenes first.
                singleton.m_NestedScenes.SetSharedComponentFilter(new SceneTag { SceneEntity = scene });
                if (!singleton.m_NestedScenes.IsEmptyIgnoreFilter)
                {
                    using (var nestedSubscenes = singleton.m_NestedScenes.ToEntityArray(Allocator.Temp))
                    {
                        for (int i=0; i<nestedSubscenes.Length; ++i)
                        {
                            SceneSystem.UnloadSceneSectionMetaEntitiesOnly(world, nestedSubscenes[i], true);
                        }
                    }
                }

                singleton.m_SceneFilter.SetSharedComponentFilter(new SceneTag { SceneEntity = scene });

                world.EntityManager.DestroyEntity(singleton.m_SceneFilter);

                singleton.m_SceneFilter.ResetFilter();

                world.EntityManager.RemoveComponent<IsSectionLoaded>(scene);
                world.EntityManager.RemoveComponent<StreamingState>(scene);
            }

            if (world.EntityManager.HasComponent<SceneSectionReferencedUnityObjects>(scene))
                world.EntityManager.RemoveComponent<SceneSectionReferencedUnityObjects>(scene);
        }

        int CreateAsyncLoadScene(Entity entity, bool blockUntilFullyLoaded)
        {
            for (int i = 0; i != m_ConcurrentSectionStreamCount; i++)
            {
                if (m_Streams[i].Operation != null)
                    continue;

                var dstManager = m_Streams[i].World.EntityManager;
                m_Streams[i].Operation = CreateAsyncLoadSceneOperation(dstManager, entity, blockUntilFullyLoaded);
                m_Streams[i].SectionEntity = entity;
                return i;
            }

            return -1;
        }

        AsyncLoadSceneOperation CreateAsyncLoadSceneOperation(EntityManager dstManager, Entity entity, bool blockUntilFullyLoaded)
        {
            var sceneData = EntityManager.GetComponentData<SceneSectionData>(entity);
            var blobHeaderOwner = EntityManager.GetSharedComponent<BlobAssetOwner>(entity);
            blobHeaderOwner.Retain();
            var sectionData = EntityManager.GetComponentData<ResolvedSectionPath>(entity);

#if !UNITY_DISABLE_MANAGED_COMPONENTS
            PostLoadCommandBuffer postLoadCommandBuffer = null;
            if (EntityManager.HasComponent<PostLoadCommandBuffer>(entity))
            {
                postLoadCommandBuffer = EntityManager.GetComponentData<PostLoadCommandBuffer>(entity);
            }
            else if (EntityManager.HasComponent<SceneEntityReference>(entity))
            {
                var sceneEntity = EntityManager.GetComponentData<SceneEntityReference>(entity).SceneEntity;
                if (EntityManager.HasComponent<PostLoadCommandBuffer>(sceneEntity))
                {
                    postLoadCommandBuffer = EntityManager.GetComponentData<PostLoadCommandBuffer>(sceneEntity);
                }
            }

            if (postLoadCommandBuffer != null)
                postLoadCommandBuffer = (PostLoadCommandBuffer)postLoadCommandBuffer.Clone();
#endif

            return new AsyncLoadSceneOperation(new AsyncLoadSceneData
            {
                ScenePath = sectionData.ScenePath.ToString(),
                SceneSize = sceneData.DecompressedFileSize,
                CompressedSceneSize = sceneData.FileSize,
                Codec = sceneData.Codec,
                EntityManager = dstManager,
                BlockUntilFullyLoaded = blockUntilFullyLoaded,
                BlobHeader = sceneData.BlobHeader,
                BlobHeaderOwner = blobHeaderOwner,
                SceneSectionEntity = entity,
                UnityObjectRefId = sectionData.HybridReferenceId,
#if !UNITY_DISABLE_MANAGED_COMPONENTS
                PostLoadCommandBuffer = postLoadCommandBuffer,
#endif
                ExternalEntitiesRefRange = sceneData.ExternalEntitiesRefRange,
                SceneSectionIndex = sceneData.SubSectionIndex,
            });
        }
    }
}
