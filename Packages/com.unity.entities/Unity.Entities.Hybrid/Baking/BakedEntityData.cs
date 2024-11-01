using System;
using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.Baking;
using Unity.Entities.Conversion;
using Unity.Entities.Hybrid.Baking;
using Unity.Jobs;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Entities
{
    //@todo: Would it be better to store this outside of it in one big NativeParallelMultiHashMap?

    //@TODO: Test for adding a condional component... (Test that reset actually runs incremental)

    internal struct PrefabState
    {
        public Hash128 GUID;
        public Hash128 Hash;
        public int RefCount;

        public PrefabState(Hash128 guid, Hash128 hash)
        {
            GUID = guid;
            Hash = hash;
            RefCount = 0;
        }
    }

    internal enum DefaultArchetype
    {
        Default,
        AdditionalEntity,
        AdditionalEntityBakeOnly,
        Prefab,
        PrefabAdditionalEntity,
        PrefabAdditionalEntityBakeOnly,
    }

    /// <summary>
    /// Stores the <see cref="BakerState"/> and the mapping of GameObject to Entity.
    /// </summary>
    /// <remarks>
    /// Responsible for playing back IncrementalBakingContext.IncrementalBakeInstructions by invoking the users
    /// <see cref="Baker{TAuthoringType}"/> code.
    /// This creates the entities and components and records dependencies as part of it.
    /// This stores what components were added by which baker so they can be reverted when a component / gameobject is removed
    /// or a dependency triggers during incremental baking.
    /// </remarks>
    unsafe struct BakedEntityData : IDisposable
    {
        /// <summary>
        /// The InstanceID of the authoring component or GameObject mapped to the BakerState.
        /// </summary>
        UnsafeParallelHashMap<int, BakerState>                       _AuthoringIDToBakerState;
        UnsafeParallelHashMap<Entity, TransformUsageFlagCounters>    _ReferencedEntities;
        bool                                                 _IsReferencedEntitiesDirty;
        UnsafeParallelHashMap<int, int>                              _ComponentToAdditionalEntityCounter;
        UnsafeParallelHashSet<int>                                   _AdditionalGameObjectsToBake;
        UnsafeParallelHashMap<int, PrefabState>                      _PrefabStates;
        BakerDebugState                                              _BakerDebugState;

        // GameObject => Entity
        internal UnsafeParallelHashMap<int, Entity>                  _GameObjectToEntity;
        internal EntityManager                               _EntityManager;
        internal EntityArchetype                             _DefaultArchetype;
        internal EntityArchetype                             _DefaultArchetypeAdditionalEntity;
        internal EntityArchetype                             _DefaultArchetypeAdditionalEntityBakeOnly;
        internal EntityArchetype                             _DefaultArchetypePrefab;
        internal EntityArchetype                             _DefaultArchetypePrefabAdditionalEntity;
        internal EntityArchetype                             _DefaultArchetypePrefabAdditionalEntityBakeOnly;
        EntityQuery                                          _HasRemoveEntityInBake;
        EntityQuery                                          _AllBakedQuery;
        EntityQuery                                          _NewPrimaryEntitiesWithAdditionalEntities;
        EntityQuery                                          _StaticEntities;
        EntityQuery                                          _DisabledEntities;
        uint                                                 _EntityGUIDNameSpaceID;
        bool                                                 _AssignEntityGUID;
        private bool                                         _CheckRevertPrefabs;
        internal Hash128                                     _SceneGUID;
        internal BakingUtility.BakingFlags _ConversionFlags;


        static string s_CreateEntityForGameObjectStr = "Baking.CreateEntityForGameObject";
        static string s_DestroyEntityForGameObjectStr = "Baking.DestroyEntityForGameObject";
        static string s_RevertStr = "Baking.Revert";
        static string s_RevertComponentsStr = "Baking.RevertComponents";
        static string s_RevertDependenciesStr = "Baking.RevertDependencies";
        static string s_RenameStr = "Baking.Rename";
        static string s_BakeStr = "Baking.Bake";
        static string s_RegisterDependenciesStr = "Baking.RegisterDependencies";
        static string s_CreateBakerStateStr = "Baking.CreateBakerState";
        static string s_BuilderPlaybackStr = "Baking.BuilderPlayback";
        static string s_AdditionalEntitiesApplyStaticActiveStateStr = "Baking.AdditionalEntitiesApplyStaticActiveState";

        static ProfilerMarker s_CreateEntityForGameObject = new ProfilerMarker(s_CreateEntityForGameObjectStr);
        static ProfilerMarker s_DestroyEntityForGameObject = new ProfilerMarker(s_DestroyEntityForGameObjectStr);
        static ProfilerMarker s_Revert = new ProfilerMarker(s_RevertStr);
        static ProfilerMarker s_RevertComponents = new ProfilerMarker(s_RevertComponentsStr);
        static ProfilerMarker s_RevertDependencies = new ProfilerMarker(s_RevertDependenciesStr);
        static ProfilerMarker s_Rename = new ProfilerMarker(s_RenameStr);
        static ProfilerMarker s_Bake = new ProfilerMarker(s_BakeStr);
        static ProfilerMarker s_RegisterDependencies = new ProfilerMarker(s_RegisterDependenciesStr);
        static ProfilerMarker s_CreateBakerState = new ProfilerMarker(s_CreateBakerStateStr);

        static ProfilerMarker s_BuilderPlayback = new ProfilerMarker(s_BuilderPlaybackStr);
        static ProfilerMarker s_AdditionalEntitiesApplyStaticActiveState = new ProfilerMarker(s_AdditionalEntitiesApplyStaticActiveStateStr);

        internal static string[] CollectImportantProfilerMarkerStrings()
        {
            return new string [] {
                s_CreateEntityForGameObjectStr,
                s_DestroyEntityForGameObjectStr,
                s_RevertStr,
                s_RevertComponentsStr,
                s_RevertDependenciesStr,
                s_RenameStr,
                s_BakeStr,
                s_RegisterDependenciesStr,
                s_CreateBakerStateStr,
                s_BuilderPlaybackStr
            };
        }

        public BakedEntityData(EntityManager manager)
        {
            _AuthoringIDToBakerState = new UnsafeParallelHashMap<int, BakerState>(10, Allocator.Persistent);
            _ComponentToAdditionalEntityCounter = new UnsafeParallelHashMap<int, int>(10, Allocator.Persistent);
            _AdditionalGameObjectsToBake = new UnsafeParallelHashSet<int>(10, Allocator.Persistent);
            _PrefabStates = new UnsafeParallelHashMap<int, PrefabState>(10, Allocator.Persistent);
            _GameObjectToEntity = new UnsafeParallelHashMap<int, Entity>(10, Allocator.Persistent);
            _ReferencedEntities = new UnsafeParallelHashMap<Entity, TransformUsageFlagCounters>(10, Allocator.Persistent);
            _BakerDebugState = new BakerDebugState(Allocator.Persistent);
            _EntityManager = manager;
            _DefaultArchetype = default;
            _DefaultArchetypeAdditionalEntity = default;
            _DefaultArchetypeAdditionalEntityBakeOnly = default;
            _DefaultArchetypePrefab = default;
            _DefaultArchetypePrefabAdditionalEntity = default;
            _DefaultArchetypePrefabAdditionalEntityBakeOnly = default;
            _SceneGUID = default;
            _AssignEntityGUID = false;
            _CheckRevertPrefabs = false;
            _EntityGUIDNameSpaceID = 0;
            _IsReferencedEntitiesDirty = true;
            _ConversionFlags = default;
            var qb = new EntityQueryBuilder(Allocator.Temp);
            _HasRemoveEntityInBake = qb.WithAll<TransformAuthoring, RemoveUnusedEntityInBake>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
                .Build(manager);
            qb.Reset();

            _AllBakedQuery = qb.WithAll<TransformAuthoring>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
                .Build(manager);
            qb.Reset();

            _NewPrimaryEntitiesWithAdditionalEntities = qb.WithAll<BakedEntity>()
                .WithAllRW<AdditionalEntitiesBakingData>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
                .Build(manager);
            qb.Reset();

            _StaticEntities = qb.WithAll<Transforms.Static>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
                .Build(manager);
            qb.Reset();

            _DisabledEntities = qb.WithAll<Disabled>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
                .Build(manager);
        }

        public void Dispose()
        {
            foreach (var context in _AuthoringIDToBakerState)
                context.Value.Dispose();
            _AuthoringIDToBakerState.Dispose();
            _AdditionalGameObjectsToBake.Dispose();
            _PrefabStates.Dispose();
            _ComponentToAdditionalEntityCounter.Dispose();
            _GameObjectToEntity.Dispose();
            _ReferencedEntities.Dispose();
            _BakerDebugState.Dispose();
        }

        public void Clear()
        {
            foreach (var context in _AuthoringIDToBakerState)
                context.Value.Dispose();
            _AuthoringIDToBakerState.Clear();
            _AdditionalGameObjectsToBake.Clear();
            _PrefabStates.Clear();
            _ComponentToAdditionalEntityCounter.Clear();
            _GameObjectToEntity.Clear();
            _ReferencedEntities.Clear();
            _BakerDebugState.Clear();
            _IsReferencedEntitiesDirty = true;
        }

        public bool IsCreated
        {
            get { return _AuthoringIDToBakerState.IsCreated; }
        }

        public void ConfigureDefaultArchetypes(BakingSettings settings, Scene scene)
        {
            var types = stackalloc ComponentType[16];
            int count = 0;
            types[count++] = ComponentType.ReadWrite<TransformAuthoring>();
            if ((settings.BakingFlags & BakingUtility.BakingFlags.AddEntityGUID) != 0)
                types[count++] = ComponentType.ReadWrite<EntityGuid>();
            if (settings.SceneGUID != default)
                types[count++] = ComponentType.ReadWrite<SceneSection>();

            // all entities changed by a baker are tagged with the BakedEntity component
            types[count++] = ComponentType.ReadWrite<BakedEntity>();

            // Archetypes for additional entities
            ConfigureDefaultArchetype(types, count, DefaultArchetype.AdditionalEntity);
            ConfigureDefaultArchetype(types, count, DefaultArchetype.AdditionalEntityBakeOnly);
            ConfigureDefaultArchetype(types, count, DefaultArchetype.PrefabAdditionalEntity);
            ConfigureDefaultArchetype(types, count, DefaultArchetype.PrefabAdditionalEntityBakeOnly);

            // AdditionalEntitiesBakingData contains a buffer of additional entities on primary entities only
            // This will add AdditionalEntitiesBakingData to the default and prefab archetypes
            types[count++] = ComponentType.ReadWrite<AdditionalEntitiesBakingData>();

            ConfigureDefaultArchetype(types, count, DefaultArchetype.Default);
            ConfigureDefaultArchetype(types, count, DefaultArchetype.Prefab);

            _EntityGUIDNameSpaceID = settings.NamespaceID ^ (uint) scene.handle;
            _AssignEntityGUID =
                (settings.BakingFlags & BakingUtility.BakingFlags.AddEntityGUID) != 0;
            _SceneGUID = settings.SceneGUID;
            _ConversionFlags = settings.BakingFlags;
        }


        public void ConfigureDefaultArchetype(ComponentType* baseType, int count, DefaultArchetype defaultArchetype)
        {
            switch (defaultArchetype)
            {
                case DefaultArchetype.Default:
                    _DefaultArchetype = _EntityManager.CreateArchetype(baseType, count);
                    break;
                case DefaultArchetype.AdditionalEntity:
                    baseType[count++] = ComponentType.ReadWrite<AdditionalEntityParent>();
                    _DefaultArchetypeAdditionalEntity = _EntityManager.CreateArchetype(baseType, count);
                    break;
                case DefaultArchetype.AdditionalEntityBakeOnly:
                    baseType[count++] = ComponentType.ReadWrite<AdditionalEntityParent>();
                    baseType[count++] = ComponentType.ReadWrite<BakingOnlyEntity>();
                    _DefaultArchetypeAdditionalEntityBakeOnly = _EntityManager.CreateArchetype(baseType, count);
                    break;
                case DefaultArchetype.Prefab:
                    baseType[count++] = ComponentType.ReadWrite<Prefab>();
                    _DefaultArchetypePrefab = _EntityManager.CreateArchetype(baseType, count);
                    break;
                case DefaultArchetype.PrefabAdditionalEntity:
                    baseType[count++] = ComponentType.ReadWrite<Prefab>();
                    baseType[count++] = ComponentType.ReadWrite<AdditionalEntityParent>();
                    _DefaultArchetypePrefabAdditionalEntity = _EntityManager.CreateArchetype(baseType, count);
                    break;
                case DefaultArchetype.PrefabAdditionalEntityBakeOnly:
                    baseType[count++] = ComponentType.ReadWrite<Prefab>();
                    baseType[count++] = ComponentType.ReadWrite<AdditionalEntityParent>();
                    baseType[count++] = ComponentType.ReadWrite<BakingOnlyEntity>();
                    _DefaultArchetypePrefabAdditionalEntityBakeOnly = _EntityManager.CreateArchetype(baseType, count);
                    break;
            }
        }

        //Method used by EntityBehaviour in motion
        internal void ConfigureEntityBehaviourDefaultArchetypes(BakingSettings settings, Scene scene)
        {
            var types = stackalloc ComponentType[16];
            int count = 0;
            //types[count++] = ComponentType.ReadWrite<TransformAuthoring>();
            if ((settings.BakingFlags & BakingUtility.BakingFlags.AddEntityGUID) != 0)
                types[count++] = ComponentType.ReadWrite<EntityGuid>();
            if (settings.SceneGUID != default)
                types[count++] = ComponentType.ReadWrite<SceneSection>();

            // all entities changed by a baker are tagged with the BakedEntity component
            //types[count++] = ComponentType.ReadWrite<BakedEntity>();

            // Archetypes for additional entities
            ConfigureEntityBehaviourDefaultArchetype(types, count, DefaultArchetype.AdditionalEntity);
            ConfigureEntityBehaviourDefaultArchetype(types, count, DefaultArchetype.AdditionalEntityBakeOnly);
            ConfigureEntityBehaviourDefaultArchetype(types, count, DefaultArchetype.PrefabAdditionalEntity);
            ConfigureEntityBehaviourDefaultArchetype(types, count, DefaultArchetype.PrefabAdditionalEntityBakeOnly);

            // AdditionalEntitiesBakingData contains a buffer of additional entities on primary entities only
            // This will add AdditionalEntitiesBakingData to the default and prefab archetypes
            types[count++] = ComponentType.ReadWrite<AdditionalEntitiesBakingData>();

            ConfigureEntityBehaviourDefaultArchetype(types, count, DefaultArchetype.Default);
            ConfigureEntityBehaviourDefaultArchetype(types, count, DefaultArchetype.Prefab);

            _EntityGUIDNameSpaceID = settings.NamespaceID ^ (uint) scene.handle;
            _AssignEntityGUID =
                (settings.BakingFlags & BakingUtility.BakingFlags.AddEntityGUID) != 0;
            _SceneGUID = settings.SceneGUID;
            _ConversionFlags = settings.BakingFlags;
        }

        //Method used by EntityBehaviour in motion
        internal void ConfigureEntityBehaviourDefaultArchetype(ComponentType* baseType, int count, DefaultArchetype defaultArchetype)
        {
            switch (defaultArchetype)
            {
                case DefaultArchetype.Default:
                    _DefaultArchetype = _EntityManager.CreateArchetype(baseType, count);
                    break;
                case DefaultArchetype.AdditionalEntity:
                    baseType[count++] = ComponentType.ReadWrite<AdditionalEntityParent>();
                    _DefaultArchetypeAdditionalEntity = _EntityManager.CreateArchetype(baseType, count);
                    break;
                case DefaultArchetype.AdditionalEntityBakeOnly:
                    baseType[count++] = ComponentType.ReadWrite<AdditionalEntityParent>();
                    //baseType[count++] = ComponentType.ReadWrite<BakingOnlyEntity>();
                    _DefaultArchetypeAdditionalEntityBakeOnly = _EntityManager.CreateArchetype(baseType, count);
                    break;
                case DefaultArchetype.Prefab:
                    baseType[count++] = ComponentType.ReadWrite<Prefab>();
                    _DefaultArchetypePrefab = _EntityManager.CreateArchetype(baseType, count);
                    break;
                case DefaultArchetype.PrefabAdditionalEntity:
                    baseType[count++] = ComponentType.ReadWrite<Prefab>();
                    baseType[count++] = ComponentType.ReadWrite<AdditionalEntityParent>();
                    _DefaultArchetypePrefabAdditionalEntity = _EntityManager.CreateArchetype(baseType, count);
                    break;
                case DefaultArchetype.PrefabAdditionalEntityBakeOnly:
                    baseType[count++] = ComponentType.ReadWrite<Prefab>();
                    baseType[count++] = ComponentType.ReadWrite<AdditionalEntityParent>();
                    //baseType[count++] = ComponentType.ReadWrite<BakingOnlyEntity>();
                    _DefaultArchetypePrefabAdditionalEntityBakeOnly = _EntityManager.CreateArchetype(baseType, count);
                    break;
            }
        }

        public NativeList<int> RemoveInvalidEntities(Allocator allocator)
        {
            NativeList<int> gameObjectsNoEntity = new NativeList<int>(100, allocator);

            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EntityGuid>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
                .Build(_EntityManager);
            Assert.IsFalse(query.HasFilter(), "The use of EntityQueryMask in this job will not respect the query's active filter settings.");
            var mask = query.GetEntityQueryMask();

            // Check if there is any invalid entity stored in _GameObjectToEntity
            var job = new FindDeletedEntitiesJob()
            {
                GameObjectEntities = _GameObjectToEntity,
                Mask = mask,
                DeletedList = gameObjectsNoEntity
            };
            job.Run();

            // Delete invalid entities from _GameObjectToEntity
            foreach (var gameObject in gameObjectsNoEntity)
            {
                _GameObjectToEntity.Remove(gameObject);
            }

            return gameObjectsNoEntity;
        }

        [BurstCompile]
        struct FindDeletedEntitiesJob : IJob
        {
            [ReadOnly] public UnsafeParallelHashMap<int, Entity> GameObjectEntities;

            [ReadOnly] public EntityQueryMask Mask;

            [NativeDisableParallelForRestriction]
            public NativeList<int> DeletedList;

            public void Execute()
            {
                foreach (var entry in GameObjectEntities)
                {
                    var entity = entry.Value;
                    if (!Mask.MatchesIgnoreFilter(entity))
                    {
                        var gameObjectID = entry.Key;
                        DeletedList.Add(gameObjectID);
                    }
                }
            }
        }

#if UNITY_EDITOR
        public void UpdatePrefabs(IncrementalBakingChangeTracker changeTracker)
        {
            foreach (var kvp in _PrefabStates)
            {
                var instanceId = kvp.Key;
                var prefabState = kvp.Value;
                var newHash = (Hash128)UnityEditor.AssetDatabase.GetAssetDependencyHash(prefabState.GUID);

                if (newHash != prefabState.Hash)
                {
                    if (newHash == default)
                    {
                        // Prefab was deleted, so mark for delete
                        changeTracker.MarkRemoved(instanceId);
                    }
                    else
                    {
                        // Prefab was changed, so force rebake
                        changeTracker.MarkForceBakeHierarchy(instanceId);
                    }
                }
            }
        }
#endif

        public void ApplyBakeInstructions(ref BakeDependencies dependencies, IncrementalBakingContext.IncrementalBakeInstructions instructions, BlobAssetStore blobAssetStore, BakingSettings bakingSettings, ref GameObjectComponents components)
        {
            // Run the actual baking instructions, and record into an EntityCommandBuffer

            // The state reverts have to be recorded in a separate ECB in order to not interact with the state changes
            // See jira DOTS-7377 for details
            var revertEcb = new EntityCommandBuffer(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Revert all components or additional entities added by previous bakers
            using (s_Revert.Auto())
            {
                foreach (var component in instructions.RevertComponents)
                {
                    if (_AuthoringIDToBakerState.TryGetValue(component, out var bakerState))
                    {
                        ResetComponentAdditionalEntityCount(component, bakerState.GetPrimaryEntity());

                        using (s_RevertDependencies.Auto())
                            BakeDependencies.ResetBakerDependencies(component, ref dependencies, ref bakerState.Dependencies);

                        using (s_RevertComponents.Auto())
                        {
                            bakerState.Revert(revertEcb, default, ref _ReferencedEntities, blobAssetStore, ref _BakerDebugState, ref _IsReferencedEntitiesDirty, ref this);
                            bakerState.Usage.Revert(default, ref _ReferencedEntities, ref _IsReferencedEntitiesDirty);
                        }

                        bakerState.Dispose();
                        _AuthoringIDToBakerState.Remove(component);

                        // Debug.Log($"ApplyInstruction - RevertBake: {component}");
                    }
                }
            }

            using (s_Rename.Auto())
            {
                foreach (var gameObject in instructions.PotentiallyRenamedGameObjects)
                {
                    if (_GameObjectToEntity.TryGetValue(gameObject.GetInstanceID(), out var entity))
                    {
                        // Debug.Log($"ApplyInstruction - Possible Rename: {component}");
                        _EntityManager.SetName(entity, gameObject.name);
                    }
                }
            }

            // Create primary entity for every game objects that were created
            using (s_CreateEntityForGameObject.Auto())
            {
                var count = instructions.CreatedGameObjects.Count;
                var entities = _EntityManager.CreateEntity(_DefaultArchetype, count, Allocator.Temp);
                for (int index = 0; index < count; ++index)
                {
                    var gameObject = instructions.CreatedGameObjects[index];
                    var entity = entities[index];
                    CreateEntityForGameObject(gameObject, entity, 0);

                    var didAdd = _GameObjectToEntity.TryAdd(gameObject.GetInstanceID(), entity);
                    if (!didAdd)
                        Debug.LogError("Internally inconsistent _GameObjectToEntity table");
                    // Debug.Log($"ApplyInstruction - CreateEntity: {entity} GameObject: {gameObject.GetInstanceID()} ({gameObject.name})");
                }
            }

            // Destroy primary entity for every game object that was destroyed
            using (s_DestroyEntityForGameObject.Auto())
            {
                foreach (var gameObject in instructions.DestroyedGameObjects)
                {
                    //@TODO: DOTS-5446
                    _GameObjectToEntity.TryGetValue(gameObject, out var entity);
                    _GameObjectToEntity.Remove(gameObject);
                    _EntityManager.DestroyEntity(entity);

                    // We might be a prefab, so we can just remove from the Prefab data with the key
                    _PrefabStates.Remove(gameObject);
                    //Debug.Log($"ApplyInstruction - DestroyEntity: {entity} GameObject: {gameObject})");

                    if (_AuthoringIDToBakerState.TryGetValue(gameObject, out var bakerState))
                    {
                        using (s_RevertDependencies.Auto())
                            BakeDependencies.ResetBakerDependencies(gameObject, ref dependencies, ref bakerState.Dependencies);
                    }
                }
            }

            var tempDependencies = new BakeDependencies.RecordedDependencies(16, Allocator.TempJob);
            var tempUsage = new BakerEntityUsage(default, 16, Allocator.TempJob);
            var revertTransformsList = new NativeList<Entity>(100, Allocator.TempJob);

            // Base size on created game objects, as worse case is initial bake and this will avoid too many allocations in that case
            var entitiesBaked = new NativeParallelHashSet<Entity>(instructions.CreatedGameObjects.Count, Allocator.Temp);

            fixed (BakerDebugState* bakerDebugState = &_BakerDebugState)
            {
                // debug state used to track duplicate destination component adds
                IBaker.BakerExecutionState state;
                state.Ecb = ecb;
                state.World = _EntityManager.World;
                state.BakedEntityData = (BakedEntityData*) UnsafeUtility.AddressOf(ref this);
                state.DebugState = bakerDebugState;
                state.BlobAssetStore = blobAssetStore;
#if UNITY_EDITOR
                state.DotsSettings = bakingSettings.DotsSettings;
#endif

                using (s_Bake.Auto())
                {
                    // Revert as a single pass all the component bakers that are going to rebake
                    foreach (var component in instructions.BakeComponents)
                    {
                        var instanceID = component.ComponentID;
                        if (_AuthoringIDToBakerState.TryGetValue(instanceID, out var bakerState))
                        {
                            if (!_GameObjectToEntity.TryGetValue(component.GameObjectInstanceID, out var entity) ||
                                !_EntityManager.Exists(entity))
                            {
                                Debug.LogError(
                                    $"Baking entity that doesn't exist: {entity} GameObject: {component.GameObjectInstanceID} Component: {component}",
                                    (GameObject) Resources.InstanceIDToObject(component.GameObjectInstanceID));
                                continue;
                            }

                            ResetComponentAdditionalEntityCount(instanceID, entity);
                            bakerState.Revert(revertEcb, entity, ref _ReferencedEntities, blobAssetStore,
                                ref _BakerDebugState, ref _IsReferencedEntitiesDirty, ref this);
                            _AuthoringIDToBakerState[instanceID] = bakerState;
                        }
                    }

                    // Revert as a single pass all the GameObject bakers that are going to rebake
                    foreach (var gameObject in instructions.BakeGameObjects)
                    {
                        var instanceID = gameObject.GetInstanceID();
                        if (_AuthoringIDToBakerState.TryGetValue(instanceID, out var bakerState))
                        {
                            if (!_GameObjectToEntity.TryGetValue(instanceID, out var entity) ||
                                !_EntityManager.Exists(entity))
                            {
                                Debug.LogError($"Baking entity that doesn't exist: {entity} GameObject: {instanceID}",
                                    gameObject);
                                continue;
                            }

                            ResetComponentAdditionalEntityCount(instanceID, entity);
                            bakerState.Revert(revertEcb, entity, ref _ReferencedEntities, blobAssetStore,
                                ref _BakerDebugState, ref _IsReferencedEntitiesDirty, ref this);
                            _AuthoringIDToBakerState[instanceID] = bakerState;
                        }
                    }

                    var gameObjectTypeIndex = TypeManager.GetTypeIndex(typeof(UnityEngine.GameObject));
                    var gameObjectBakers = BakerDataUtility.GetBakers(gameObjectTypeIndex);
                    if (gameObjectBakers != null)
                    {
#if UNITY_EDITOR
#if ENABLE_CLOUD_SERVICES_ANALYTICS
                        // Save the data for Analytics
                        BakingAnalytics.LogBakerTypeIndex(gameObjectTypeIndex);
#endif
#endif

                        // bake the GameObject properties
                        foreach (var gameObject in instructions.BakeGameObjects)
                        {
                            var instanceID = gameObject.GetInstanceID();
                            if (!_GameObjectToEntity.TryGetValue(instanceID, out var entity) ||
                                !_EntityManager.Exists(entity))
                            {
                                Debug.LogError($"Baking entity that doesn't exist: {entity} GameObject: {instanceID}",
                                    gameObject);
                                continue;
                            }

                            entitiesBaked.Add(entity);

                            var didExist = _AuthoringIDToBakerState.TryGetValue(instanceID, out var bakerState);
                            try
                            {
                                // Need full revert / rebake
                                if (didExist)
                                {
                                    tempDependencies.Clear();
                                    tempUsage.Clear(entity);

                                    for (int i = 0, n = gameObjectBakers.Length; i < n; ++i)
                                    {
                                        var baker = gameObjectBakers[i];

                                        // We don't run bake if the baker belongs to a disabled assembly
                                        if (!baker.AssemblyData.Enabled)
                                            continue;

                                        using (baker.Profiler.Auto())
                                        {
                                            try
                                            {
                                                state.Usage = &tempUsage;
                                                state.Dependencies = &tempDependencies;
                                                state.DebugIndex.TypeIndex = gameObjectTypeIndex;
                                                state.DebugIndex.IndexInBakerArray = i;
                                                state.BakerState = &bakerState;
                                                state.AuthoringSource = null;
                                                state.AuthoringObject = gameObject;
                                                state.AuthoringId = gameObject.GetInstanceID();
                                                state.PrimaryEntity = entity;

                                                // baker.Baker.BakeInternal(ref tempDependencies, ref tempUsage, ref bakerState, ref _BakerDebugState, i, ref this, ref ecb, component.Component, blobAssetStore);
                                                baker.Baker.InvokeBake(state);
                                            }
                                            catch (Exception e)
                                            {
                                                Debug.LogException(e);
                                            }
                                        }
                                    }

                                    using (s_RegisterDependencies.Auto())
                                    {
                                        UpdateDependencies(ref dependencies, instanceID, ref bakerState, ref tempDependencies, ref tempUsage, out var revertTransforms);
                                        if (revertTransforms)
                                        {
                                            revertTransformsList.Add(tempUsage.PrimaryEntity);
                                        }
                                    }
                                }
                                else
                                {
                                    bool hasBaked = false;
                                    using (s_CreateBakerState.Auto())
                                    {
                                        bakerState = new BakerState(entity, Allocator.Persistent);
                                    }
                                    for (int i = 0, n = gameObjectBakers.Length; i < n; ++i)
                                    {
                                        var baker = gameObjectBakers[i];

                                        // We don't run bake if the baker belongs to a disabled assembly
                                        if (!baker.AssemblyData.Enabled)
                                            continue;

                                        using (baker.Profiler.Auto())
                                        {
                                            try
                                            {
                                                state.Usage = &bakerState.Usage;
                                                state.Dependencies = &bakerState.Dependencies;
                                                state.DebugIndex.TypeIndex = gameObjectTypeIndex;
                                                state.DebugIndex.IndexInBakerArray = i;
                                                state.BakerState = &bakerState;
                                                state.AuthoringSource = null;
                                                state.AuthoringObject = gameObject;
                                                state.AuthoringId = gameObject.GetInstanceID();
                                                state.PrimaryEntity = entity;

                                                // baker.Baker.BakeInternal(ref tempDependencies, ref tempUsage, ref bakerState, ref _BakerDebugState, i, ref this, ref ecb, component.Component, blobAssetStore);
                                                baker.Baker.InvokeBake(state);
                                                hasBaked = true;
                                            }
                                            catch (Exception e)
                                            {
                                                Debug.LogException(e);
                                            }
                                        }
                                    }

                                    if (hasBaked)
                                    {
                                        using (s_RegisterDependencies.Auto())
                                        {
                                            AddDependencies(ref dependencies, instanceID, ref bakerState, out var revertTransforms);
                                            if (revertTransforms)
                                            {
                                                revertTransformsList.Add(bakerState.PrimaryEntity);
                                            }
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                _AuthoringIDToBakerState[instanceID] = bakerState;
                            }
                        }
                    }

                    // bake new and changed components
                    foreach (var component in instructions.BakeComponents)
                    {
                        var instanceID = component.ComponentID;

                        _GameObjectToEntity.TryGetValue(component.GameObjectInstanceID, out var entity);
                        if (!_EntityManager.Exists(entity))
                            Debug.LogError(
                                $"Baking entity that doesn't exist: {entity} GameObject: {component.GameObjectInstanceID} Component: {component}",
                                (GameObject) Resources.InstanceIDToObject(component.GameObjectInstanceID));

#if UNITY_EDITOR
                        if (bakingSettings != null && bakingSettings.BakingSystemFilterSettings != null)
                        {
                            BakerDataUtility.ApplyAssemblyFilter(bakingSettings.BakingSystemFilterSettings);
                        }
#endif

                        var bakeTypeIndex = TypeManager.GetTypeIndex(component.Component.GetType());
                        var bakers = BakerDataUtility.GetBakers(bakeTypeIndex);
                        if (bakers == null)
                            continue;
#if UNITY_EDITOR
#if ENABLE_CLOUD_SERVICES_ANALYTICS
                        // Save the data for Analytics
                        BakingAnalytics.LogBakerTypeIndex(bakeTypeIndex);
#endif
#endif
                        entitiesBaked.Add(entity);
                        var didExist = _AuthoringIDToBakerState.TryGetValue(instanceID, out var bakerState);
                        try
                        {
                            // Need full revert / rebake
                            if (didExist)
                            {
                                tempDependencies.Clear();
                                tempUsage.Clear(entity);

                                // We don't bake disabled components
                                bool isDisabled = component.Component.IsComponentDisabled();

                                // Rebake all bakers for this component
                                for (int i = 0, n = bakers.Length; i < n; ++i)
                                {
                                    var baker = bakers[i];

                                    // We don't run bake if the baker belongs to a disabled assembly, unless this Baker is enforcing it specifically
                                    if (!baker.AssemblyData.Enabled || (isDisabled && !baker.ForceBakingForDisabledComponents))
                                        continue;

                                    using (baker.Profiler.Auto())
                                    {
                                        try
                                        {
                                            state.Usage = &tempUsage;
                                            state.Dependencies = &tempDependencies;
                                            state.DebugIndex.TypeIndex = bakeTypeIndex;
                                            state.DebugIndex.IndexInBakerArray = i;
                                            state.BakerState = &bakerState;
                                            state.AuthoringSource = component.Component;
                                            state.AuthoringObject = component.Component.gameObject;
                                            state.AuthoringId = component.Component.GetInstanceID();
                                            state.PrimaryEntity = entity;

                                            // baker.Baker.BakeInternal(ref tempDependencies, ref tempUsage, ref bakerState, ref _BakerDebugState, i, ref this, ref ecb, component.Component, blobAssetStore);
                                            baker.Baker.InvokeBake(state);
                                        }
                                        catch (Exception e)
                                        {
                                            Debug.LogException(e);
                                        }
                                    }
                                }

                                // We need to update the dependencies even if the component is disabled
                                using (s_RegisterDependencies.Auto())
                                {
                                    UpdateDependencies(ref dependencies, instanceID, ref bakerState, ref tempDependencies, ref tempUsage, out var revertTransforms);
                                    if (revertTransforms)
                                    {
                                        revertTransformsList.Add(tempUsage.PrimaryEntity);
                                    }
                                }
                            }
                            // Added baker for the first time, can just add.
                            else
                            {
                                using (s_CreateBakerState.Auto())
                                {
                                    bakerState = new BakerState(entity, Allocator.Persistent);
                                }

                                // We don't bake disabled components
                                bool isDisabled = component.Component.IsComponentDisabled();
                                bool hasBaked = false;

                                // Rebake all bakers for this component
                                for (int i = 0, n = bakers.Length; i < n; ++i)
                                {
                                    var baker = bakers[i];

                                    // We don't run bake if the baker belongs to a disabled assembly, unless this Baker is enforcing it specifically
                                    if (!baker.AssemblyData.Enabled || (isDisabled && !baker.ForceBakingForDisabledComponents))
                                        continue;

                                    using (baker.Profiler.Auto())
                                    {
                                        try
                                        {
                                            state.Usage = &bakerState.Usage;
                                            state.Dependencies = &bakerState.Dependencies;
                                            state.DebugIndex.TypeIndex = bakeTypeIndex;
                                            state.DebugIndex.IndexInBakerArray = i;
                                            state.BakerState = &bakerState;
                                            state.AuthoringSource = component.Component;
                                            state.AuthoringObject = component.Component.gameObject;
                                            state.AuthoringId = component.Component.GetInstanceID();
                                            state.PrimaryEntity = entity;

                                            baker.Baker.InvokeBake(state);
                                            hasBaked = true;
                                        }
                                        catch (Exception e)
                                        {
                                            Debug.LogException(e);
                                        }
                                    }
                                }

                                if (hasBaked)
                                {
                                    using (s_RegisterDependencies.Auto())
                                    {
                                        AddDependencies(ref dependencies, instanceID, ref bakerState,
                                            out var revertTransforms);
                                        if (revertTransforms)
                                        {
                                            revertTransformsList.Add(bakerState.PrimaryEntity);
                                        }
                                    }
                                }
                            }
                        }
                        finally
                        {
                            // NOTE: Have to copy back to the baking context since BakerState is copied by value
                            // Would be better to keep a ref to the bakerState (Should come into master soon)
                            _AuthoringIDToBakerState[instanceID] = bakerState;
                        }

                        //Debug.Log($"ApplyInstruction - Bake: {entity} GameObject: {component.GameObjectInstanceID} ({Resources.InstanceIDToObject(component.GameObjectInstanceID)}) Component: {component.Component.GetInstanceID()} ({component.Component})");
                    }
                }
            }

            tempDependencies.Dispose();
            tempUsage.Dispose();

            // For all entities baked, add the BakedEntity tag
            foreach (var entity in entitiesBaked)
            {
                ecb.AddComponent<BakedEntity>(entity);
            }
            entitiesBaked.Dispose();
#if UNITY_EDITOR
#if ENABLE_CLOUD_SERVICES_ANALYTICS
            BakingAnalytics.LogPrefabCount(_PrefabStates.Count());
            BakingAnalytics.LogBlobAssetCount(blobAssetStore.BlobAssetCount);
#endif
#endif
            // Revert all the transform components for the entities moved to ManualOverride
            if (revertTransformsList.Length > 0)
            {
                var revertTransformsArray = revertTransformsList.AsArray();
                revertEcb.RemoveComponent<LocalToWorld>(revertTransformsArray);
                revertEcb.RemoveComponent<LocalTransform>(revertTransformsArray);
                revertEcb.RemoveComponent<PostTransformMatrix>(revertTransformsArray);
                revertEcb.RemoveComponent<Parent>(revertTransformsArray);
            }
            revertTransformsList.Dispose();

            // Play back the entity command buffer so it can be done in batch.
            using (s_BuilderPlayback.Auto())
            {
                // The state reverts have to be applied before the state changes.
                revertEcb.Playback(_EntityManager);
                revertEcb.Dispose();

                ecb.Playback(_EntityManager);
                ecb.Dispose();
            }
        }

        void AddDependencies(ref BakeDependencies dependencies, int instanceID, ref BakerState bakerState, out bool revertTransformComponents)
        {
            BakeDependencies.AddDependencies(ref dependencies, instanceID, ref bakerState.Dependencies);

            revertTransformComponents = false;
            if (!bakerState.Usage.PrimaryEntityFlags.IsUnused && bakerState.Usage.PrimaryEntityFlags.HasManualOverrideFlag())
            {
                if(_ReferencedEntities.TryGetValue(bakerState.Usage.PrimaryEntity, out var oldFlags))
                    revertTransformComponents = !oldFlags.HasManualOverrideFlag();
            }

            bakerState.Usage.AddTransformUsage(ref _ReferencedEntities, ref _IsReferencedEntitiesDirty, instanceID);
        }

        void UpdateDependencies(ref BakeDependencies dependencies, int instanceID, ref BakerState bakerState, ref BakeDependencies.RecordedDependencies tempDependencies, ref BakerEntityUsage tempUsage, out bool revertTransformComponents)
        {
            if (BakeDependencies.UpdateDependencies(ref dependencies, instanceID, ref bakerState.Dependencies, ref tempDependencies))
            {
                //Debug.Log($"Updating dependencies for: '{Resources.InstanceIDToObject(component.GameObjectInstanceID).name}' {component.Component.GetType().Name}");
            }

            if (BakerEntityUsage.Update(ref _ReferencedEntities, ref _IsReferencedEntitiesDirty, ref bakerState.Usage, ref tempUsage, instanceID, out revertTransformComponents))
            {
                //Debug.Log($"Updating usage for: '{Resources.InstanceIDToObject(component.GameObjectInstanceID).name}' {component.Component.GetType().Name}");
            }
        }

        [BurstCompile]
        struct UpdateReferencedEntitiesJob : IJobChunk
        {
            public EntityQueryMask  HasRemoveEntityInBake;

            public EntityTypeHandle Entities;

            public NativeList<Entity> Remove;
            public NativeList<Entity> Add;

            [ReadOnly] public UnsafeParallelHashMap<Entity, TransformUsageFlagCounters> ReferencedEntities;
            [ReadOnly] public ComponentTypeHandle<TransformAuthoring> TransformAuthoringTypeHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                var hasRemoveEntityInBake = HasRemoveEntityInBake.MatchesIgnoreFilter(chunk);

                var entities = chunk.GetNativeArray(Entities);
                var transformAuthoring = chunk.GetNativeArray(ref TransformAuthoringTypeHandle);
                for (int index = 0; index < entities.Length; ++index)
                {
                    var e = entities[index];
                    var shouldRemoveEntityInBake = (!ReferencedEntities.TryGetValue(e, out var usage) || usage.IsUnused) &&
                                                   transformAuthoring[index].RuntimeTransformUsage == RuntimeTransformComponentFlags.None;

                    if (shouldRemoveEntityInBake != hasRemoveEntityInBake)
                    {
                        if (shouldRemoveEntityInBake)
                            Add.Add(e);
                        else
                            Remove.Add(e);
                    }
                }
            }
        }

        public void UpdateReferencedEntities()
        {
            if (!_IsReferencedEntitiesDirty)
                return;
            _IsReferencedEntitiesDirty = false;

            Assert.IsFalse(_HasRemoveEntityInBake.HasFilter(), "The use of EntityQueryMask in this job will not respect the query's active filter settings.");
            var job = new UpdateReferencedEntitiesJob
            {
                Entities = _EntityManager.GetEntityTypeHandle(),
                HasRemoveEntityInBake = _HasRemoveEntityInBake.GetEntityQueryMask(),
                Add = new NativeList<Entity>(Allocator.TempJob),
                Remove = new NativeList<Entity>(Allocator.TempJob),
                ReferencedEntities = _ReferencedEntities,
                TransformAuthoringTypeHandle = _EntityManager.GetComponentTypeHandle<TransformAuthoring>(true)
            };
            job.Run(_AllBakedQuery);

            _EntityManager.RemoveComponent<RemoveUnusedEntityInBake>(job.Remove.AsArray());
            _EntityManager.AddComponent<RemoveUnusedEntityInBake>(job.Add.AsArray());

            job.Remove.Dispose();
            job.Add.Dispose();
        }

        internal Entity CreateEntityForGameObject(GameObject gameObject, int authoringInstanceId, EntityArchetype archetype, int serial = 0, string entityName = "")
        {
            if (gameObject == null)
                throw new ArgumentNullException(nameof(gameObject),
                    $"{nameof(CreateEntityForGameObject)} must be called with a valid UnityEngine.Object");

            var entity = _EntityManager.CreateEntity(archetype);
            CreateEntityForGameObject(gameObject, entity, authoringInstanceId, serial, entityName);
            return entity;
        }

        private SceneSection MakeSceneSectionComponent(GameObject gameObject)
        {
            var section = gameObject.GetComponentInParent<SceneSectionComponent>(true);
            var sectionIndex = section != null ? section.SectionIndex : 0;
            return new SceneSection { SceneGUID = _SceneGUID, Section = sectionIndex };
        }

        Entity CreateEntityForGameObject(GameObject gameObject, Entity entity, int authoringInstanceId, int serial = 0, string entityName = "")
        {
            if (gameObject == null)
                throw new ArgumentNullException(nameof(gameObject),
                    $"{nameof(CreateEntityForGameObject)} must be called with a valid UnityEngine.Object");

            if (_AssignEntityGUID)
            {
                var entityGuid = new EntityGuid(gameObject.GetInstanceID(), authoringInstanceId, _EntityGUIDNameSpaceID, (uint)serial);
                _EntityManager.SetComponentData(entity, entityGuid);
            }

            if (_SceneGUID != default)
                _EntityManager.SetSharedComponent(entity, MakeSceneSectionComponent(gameObject));

#if UNITY_EDITOR
            var AssignName = true;
            if (AssignName)
            {
                // Truncate the name to ensure it fits.
                var fs = new FixedString64Bytes();
                if(String.IsNullOrEmpty(entityName))
                    FixedStringMethods.CopyFromTruncated(ref fs, gameObject.name);
                else
                    FixedStringMethods.CopyFromTruncated(ref fs, entityName);

                _EntityManager.SetName(entity, fs);
            }
#endif

            return entity;
        }

        // Batched version of CreateEntityForGameObject
        void SetupEntitiesForGameObject(NativeArray<Entity> entities, GameObject gameObject, int authoringInstanceId, int serial)
        {
            Assert.IsTrue(gameObject != null);

            if (_AssignEntityGUID)
            {
                var goInstanceID = gameObject.GetInstanceID();
                for (int i = 0; i < entities.Length; ++i)
                {
                    var entityGuid = new EntityGuid(goInstanceID, authoringInstanceId, _EntityGUIDNameSpaceID, (uint)(serial + i));
                    _EntityManager.SetComponentData(entities[i], entityGuid);
                }
            }

            if (_SceneGUID != default)
                _EntityManager.SetSharedComponent(entities, MakeSceneSectionComponent(gameObject));

#if UNITY_EDITOR
            var AssignName = true;
            if (AssignName)
            {
                // Truncate the name to ensure it fits.
                var fs = new FixedString64Bytes();
                FixedStringMethods.CopyFromTruncated(ref fs, gameObject.name);

                foreach (var e in entities)
                    _EntityManager.SetName(e, fs);
            }
#endif
        }

        public bool HasAdditionalGameObjectsToBake()
        {
            return !_AdditionalGameObjectsToBake.IsEmpty;
        }

        public bool HasPrefabsToCheck()
        {
            return _CheckRevertPrefabs;
        }

        public NativeArray<int> GetAndClearAdditionalObjectsToBake(Allocator allocator)
        {
            var additionalObjectsToBakeArray = _AdditionalGameObjectsToBake.ToNativeArray(allocator);
            _AdditionalGameObjectsToBake.Clear();
            return additionalObjectsToBakeArray;
        }

        public NativeArray<int> GetAndClearPrefabObjectsToDestroy(ref IncrementalHierarchy hierarchy, Allocator allocator)
        {
            if (!_CheckRevertPrefabs)
                return new NativeArray<int>();

            var destroyedPrefabs = new NativeList<int>(0, Allocator.Temp);
            foreach (var kvp in _PrefabStates)
            {
                if(kvp.Value.RefCount == 0)
                {
                    destroyedPrefabs.Add(kvp.Key);
                    if (hierarchy.IndexByInstanceId.TryGetValue(kvp.Key, out var parentIndex))
                    {
                        var children = IncrementalHierarchyFunctions.GetChildrenRecursively(hierarchy, parentIndex);
                        foreach (var childIndex in children)
                            destroyedPrefabs.Add(hierarchy.InstanceId[childIndex]);
                    }
                }
            }
            var destroyedPrefabsArray = destroyedPrefabs.ToArray(allocator);
            IncrementalHierarchyFunctions.Remove(hierarchy, destroyedPrefabsArray);
            destroyedPrefabs.Dispose();
            _CheckRevertPrefabs = false;
            return destroyedPrefabsArray;
        }

#if UNITY_EDITOR
        public void AddPrefabRef(int instanceId)
        {
            if(!_PrefabStates.TryGetValue(instanceId, out var prefabState))
            {
                var prefabGUID = UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(instanceId).assetGUID;
                if (prefabGUID != default)
                {
                    //@TODO: DOTS-5441
                    var newHash = UnityEditor.AssetDatabase.GetAssetDependencyHash(prefabGUID);

                    prefabState = new PrefabState(prefabGUID, newHash);
                }
            }
            prefabState.RefCount++;
            _PrefabStates[instanceId] = prefabState;
        }

        public void RemovePrefabRef(int instanceId)
        {
            _PrefabStates.TryGetValue(instanceId, out var prefabState);
            prefabState.RefCount--;
            _PrefabStates[instanceId] = prefabState;
            _CheckRevertPrefabs = true;
        }
#endif

        internal Entity CreateEntityForPrefab(GameObject prefab)
        {
            var allTransforms = prefab.GetComponentsInChildren<Transform>(true);
            var count = allTransforms.Length;
            var entities = _EntityManager.CreateEntity(_DefaultArchetypePrefab, count, Allocator.Temp);

            var instanceId = prefab.GetInstanceID();
            var entity = entities[0];
            CreateEntityForGameObject(prefab, entity, 0, 0);
            _GameObjectToEntity[instanceId] = entity;

            // Make sure prefab root is dynamic
            var counters = new TransformUsageFlagCounters();
            counters.Add(TransformUsageFlags.Dynamic);
            _ReferencedEntities.Add(entity, counters);

            // Now register the Prefab for lazy baking
            _AdditionalGameObjectsToBake.Add(instanceId);

            // Add all children
            var linkedEntityGroupArray = new NativeArray<LinkedEntityGroupBakingData>(count, Allocator.Temp);

            // Assign self to first position in linked entity group
            linkedEntityGroupArray[0] = new LinkedEntityGroupBakingData {Value = entity};
            for(int i = 1; i < count; i++)
            {
                var childGameObject = allTransforms[i].gameObject;
                var childEntity = entities[i];
                CreateEntityForGameObject(childGameObject, childEntity, 0, 0);

                _GameObjectToEntity[childGameObject.GetInstanceID()] = childEntity;

                linkedEntityGroupArray[i] = new LinkedEntityGroupBakingData {Value = childEntity};
            }

            var buffer = _EntityManager.AddBuffer<LinkedEntityGroupBakingData>(entity);
            buffer.AddRange(linkedEntityGroupArray);
            linkedEntityGroupArray.Dispose();

            return entity;
        }

        public Entity GetEntity(GameObject gameObject)
        {
            if (gameObject == null)
                return Entity.Null;

            var gameObjectId = gameObject.GetInstanceID();

            // If it already exists, just give back the reference
            if (!_GameObjectToEntity.TryGetValue(gameObjectId, out var entity))
            {
                if (gameObject.IsPrefab())
                {
                    // Okay, it doesn't exist so we need to create it
                    entity = CreateEntityForPrefab(gameObject);
                }
            }

            return entity;
        }

        public Entity GetEntity(Component component)
        {
            return component == null ? Entity.Null : GetEntity(component.gameObject);
        }

        private EntityArchetype GetAdditionalEntityArchetype(GameObject gameObject, bool bakingOnlyEntity)
        {
            if (gameObject.IsPrefab())
                return bakingOnlyEntity ? _DefaultArchetypePrefabAdditionalEntityBakeOnly : _DefaultArchetypePrefabAdditionalEntity;
            else
                return bakingOnlyEntity ? _DefaultArchetypeAdditionalEntityBakeOnly : _DefaultArchetypeAdditionalEntity;
        }

        public Entity CreateAdditionalEntity(GameObject gameObject, int authoringInstanceId, bool bakingOnlyEntity, string entityName = "")
        {
            var instanceId = gameObject.GetInstanceID();
            var primaryEntity = _GameObjectToEntity[instanceId];

            _ComponentToAdditionalEntityCounter.TryGetValue(authoringInstanceId, out var counter);
            counter += 1;

            _ComponentToAdditionalEntityCounter[authoringInstanceId] = counter;

            var entityArchetype = GetAdditionalEntityArchetype(gameObject, bakingOnlyEntity);
            var entity = CreateEntityForGameObject(gameObject, authoringInstanceId, entityArchetype, counter, entityName);

            _EntityManager.SetComponentData(entity, new AdditionalEntityParent { Parent = primaryEntity, ParentInstanceID = instanceId });
            var buffer = _EntityManager.GetBuffer<AdditionalEntitiesBakingData>(primaryEntity);
            buffer.Add(new AdditionalEntitiesBakingData
            {
                Value = entity,
                AuthoringComponentID = authoringInstanceId
            });

            return entity;
        }

        public void CreateAdditionalEntities(NativeArray<Entity> outputEntities, GameObject gameObject, int authoringInstanceId, bool bakingOnlyEntity)
        {
            if (!outputEntities.IsCreated || outputEntities.Length == 0)
                return;

            var instanceId = gameObject.GetInstanceID();
            var primaryEntity = _GameObjectToEntity[instanceId];

            _ComponentToAdditionalEntityCounter.TryGetValue(authoringInstanceId, out var counter);
            _ComponentToAdditionalEntityCounter[authoringInstanceId] = counter + outputEntities.Length;

            var entityArchetype = GetAdditionalEntityArchetype(gameObject, bakingOnlyEntity);
            _EntityManager.CreateEntity(entityArchetype, outputEntities);
            SetupEntitiesForGameObject(outputEntities, gameObject, authoringInstanceId, counter + 1);

            var buffer = _EntityManager.GetBuffer<AdditionalEntitiesBakingData>(primaryEntity);
            int bufferWritePos = buffer.Length;
            buffer.ResizeUninitialized(bufferWritePos + outputEntities.Length);
            foreach (var e in outputEntities)
            {
                _EntityManager.SetComponentData(e, new AdditionalEntityParent { Parent = primaryEntity, ParentInstanceID = instanceId });
                buffer[bufferWritePos++] = new AdditionalEntitiesBakingData
                {
                    Value = e,
                    AuthoringComponentID = authoringInstanceId
                };
            }
        }

        public UnsafeHashSet<Entity> GetEntitiesForBakers(Component component)
        {
            var builder = _AuthoringIDToBakerState[component.GetInstanceID()];
            return builder.GetEntities();
        }

        public Entity GetPrimaryEntity(Component component)
        {
            var builder = _AuthoringIDToBakerState[component.GetInstanceID()];
            return builder.GetPrimaryEntity();
        }

        public void UpdateTransforms(TransformAuthoringBaking transformAuthoringBaking)
        {
            transformAuthoringBaking.UpdateTransforms(_GameObjectToEntity, _ReferencedEntities, ref _IsReferencedEntitiesDirty);
        }

        internal void ResetComponentAdditionalEntityCount(int authoringInstanceId, Entity entity)
        {
            _ComponentToAdditionalEntityCounter[authoringInstanceId] = 0;
            if (entity != Entity.Null && _EntityManager.HasBuffer<AdditionalEntitiesBakingData>(entity))
            {
                var buffer = _EntityManager.GetBuffer<AdditionalEntitiesBakingData>(entity);

                // Find the additional entities relative to the authoringID and remove them
                for (int index = buffer.Length - 1; index >= 0; --index)
                {
                    if (buffer[index].AuthoringComponentID == authoringInstanceId)
                    {
                        buffer.RemoveAt(index);
                    }
                }
            }
        }

        /// <summary>
        /// Applies the <see cref="Transforms.Static"/> and <see cref="Disabled"/> tags on the additional entities tied to the recently baked primary entities.
        /// </summary>
        /// <remarks>
        /// The additional entities always match the static and active state of the primary entity they are tied to.
        /// The state is enforced in a baking system rather that in the baker that created the additional entities to avoid having to rerun the baker that created
        /// the additional entities in case the authoring GameObject state changed.
        /// </remarks>
        public void AdditionalEntitiesApplyActiveStaticState()
        {
            using var scope = s_AdditionalEntitiesApplyStaticActiveState.Auto();
            using var ecb = new EntityCommandBuffer(Allocator.TempJob);

            var primaryEntities = _NewPrimaryEntitiesWithAdditionalEntities.ToEntityArray(Allocator.TempJob);

            var job = new ApplyActiveStaticStateJob
            {
                PrimaryEntities = primaryEntities,
                AdditionalEntityBufferLookup = _EntityManager.GetBufferLookup<AdditionalEntitiesBakingData>(),
                StaticMask = _StaticEntities.GetEntityQueryMask(),
                DisabledMask = _DisabledEntities.GetEntityQueryMask(),
                CommandBuffer = ecb.AsParallelWriter(),
            };
            job.ScheduleByRef(primaryEntities.Length, 32).Complete();

            ecb.Playback(_EntityManager);
        }

        [BurstCompile]
        struct ApplyActiveStaticStateJob : IJobParallelFor
        {
            [ReadOnly]
            public BufferLookup<AdditionalEntitiesBakingData> AdditionalEntityBufferLookup;
            [ReadOnly, DeallocateOnJobCompletion]
            public NativeArray<Entity> PrimaryEntities;
            public EntityQueryMask StaticMask;
            public EntityQueryMask DisabledMask;

            public EntityCommandBuffer.ParallelWriter CommandBuffer;

            public void Execute(int index)
            {
                var primaryEntity = PrimaryEntities[index];
                var additionalEntities = AdditionalEntityBufferLookup[primaryEntity];
                if (additionalEntities.IsEmpty)
                    return;

                var isStatic = StaticMask.MatchesIgnoreFilter(primaryEntity);
                var isDisabled = DisabledMask.MatchesIgnoreFilter(primaryEntity);

                for (int i = 0, count = additionalEntities.Length; i < count; ++i)
                {
                    var entity = additionalEntities[i].Value;
                    if (isStatic != StaticMask.MatchesIgnoreFilter(entity))
                    {
                        if (isStatic)
                            CommandBuffer.AddComponent<Transforms.Static>(index, entity);
                        else
                            CommandBuffer.RemoveComponent<Transforms.Static>(index, entity);
                    }

                    if (isDisabled != DisabledMask.MatchesIgnoreFilter(entity))
                    {
                        if (isDisabled)
                            CommandBuffer.AddComponent<Disabled>(index, entity);
                        else
                            CommandBuffer.RemoveComponent<Disabled>(index, entity);
                    }
                }
            }
        }
    }
}
