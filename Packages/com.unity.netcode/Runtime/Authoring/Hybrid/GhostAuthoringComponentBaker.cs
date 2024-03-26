using System;
using Unity.Entities;
using UnityEngine;
using Unity.Assertions;
using Unity.Collections;
using Unity.NetCode.Hybrid;

namespace Unity.NetCode
{
    // This struct mirrors GhostPrefabConfig, but it hasn't got any managed types so it can be used inside a regular component
    struct GhostPrefabConfigBaking
    {
        public UnityObjectRef<GhostAuthoringComponent> Authoring;
        public int Importance;
        public GhostModeMask SupportedGhostModes;
        public GhostMode DefaultGhostMode;
        public GhostOptimizationMode OptimizationMode;
        public bool UsePreSerialization;
    }

    // This type contains all the information pulled from the authoring component in the baker
    [BakingType]
    struct GhostAuthoringComponentBakingData : IComponentData
    {
        public GhostPrefabConfigBaking BakingConfig;
        public GhostType GhostType;
        public NetcodeConversionTarget Target;
        public bool IsPrefab;
        public bool IsActive;
        public FixedString64Bytes GhostName;
        public ulong GhostNameHash;
    }

    // This type is used to store the overrides
    [BakingType]
    struct GhostAuthoringComponentOverridesBaking : IBufferElementData
    {
        //For sake of serialization we are using the type fullname because we can't rely on the TypeIndex for the component.
        //StableTypeHash cannot be used either because layout or fields changes affect the hash too (so is not a good candidate for that)
        public ulong FullTypeNameID;
        //The gameObject reference (root or child)
        public int GameObjectID;
        //The entity guid reference
        public ulong EntityGuid;
        //Override what mode are available for that type. if 0, the component is removed from the prefab/entity instance
        public int PrefabType;
        //Override which client it will be sent to.
        public int SendTypeOptimization;
        //Select which variant we would like to use. 0 means the default
        public ulong ComponentVariant;
    }

    [BakingVersion("cmarastoni", 1)]
    class GhostAuthoringComponentBaker : Baker<GhostAuthoringComponent>
    {
        public override void Bake(GhostAuthoringComponent ghostAuthoring)
        {
            var ghostName = ghostAuthoring.GetAndValidateGhostName(out var ghostNameHash);

            // Prefab dependency
            bool isPrefab = !ghostAuthoring.gameObject.scene.IsValid() || ghostAuthoring.ForcePrefabConversion;
#if UNITY_EDITOR
            if (!isPrefab)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(ghostAuthoring.prefabId);
                GameObject prefab = null;
                if (!String.IsNullOrEmpty(path))
                    prefab = (GameObject) UnityEditor.AssetDatabase.LoadAssetAtPath(path, typeof(GameObject));
                // GetEntity is used here to tell the baker that the prefab needs to be baked as well. This replaces
                // the Conversion callback DeclareReferencedPrefabs.
                GetEntity(prefab, TransformUsageFlags.Dynamic);
            }
#endif

            //There are some issue with conversion at runtime in some occasions we cannot use PrefabStage checks or similar here
            var target = this.GetNetcodeTarget(isPrefab);
            // Check if the ghost is valid before starting to process
            if (String.IsNullOrEmpty(ghostAuthoring.prefabId))
                throw new InvalidOperationException($"The ghost {ghostName} is not a valid prefab, all ghosts must be the top-level GameObject in a prefab. Ghost instances in scenes must be instances of such prefabs and changes should be made on the prefab asset, not the prefab instance");

            if (!isPrefab && ghostAuthoring.DefaultGhostMode == GhostMode.OwnerPredicted && target != NetcodeConversionTarget.Server)
                throw new InvalidOperationException($"Cannot convert a owner predicted ghost {ghostName} as a scene instance");

            if (!isPrefab && IsClient())
                throw new InvalidOperationException($"The ghost {ghostName} cannot be created on the client, either put it in a sub-scene or spawn it on the server only");

            if (ghostAuthoring.prefabId.Length != 32)
                throw new InvalidOperationException("Invalid guid for ghost prefab type");

            // Add components which are serialized based on settings
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            if (ghostAuthoring.HasOwner)
            {
                AddComponent(entity, default(GhostOwner));
                AddComponent(entity, default(GhostOwnerIsLocal));
            }
            if (ghostAuthoring.SupportAutoCommandTarget && ghostAuthoring.HasOwner)
                AddComponent(entity, new AutoCommandTarget {Enabled = true});
            if (ghostAuthoring.TrackInterpolationDelay && ghostAuthoring.HasOwner)
                AddComponent(entity, default(CommandDataInterpolationDelay));
            if (ghostAuthoring.GhostGroup)
                AddBuffer<GhostGroup>(entity);

            var allComponentOverrides = GhostAuthoringInspectionComponent.CollectAllComponentOverridesInInspectionComponents(ghostAuthoring, true);

            var overrideBuffer = AddBuffer<GhostAuthoringComponentOverridesBaking>(entity);
            foreach (var componentOverride in allComponentOverrides)
            {
                overrideBuffer.Add(new GhostAuthoringComponentOverridesBaking
                {
                    FullTypeNameID = TypeManager.CalculateFullNameHash(componentOverride.Item2.FullTypeName),
                    GameObjectID = componentOverride.Item1.GetInstanceID(),
                    EntityGuid = componentOverride.Item2.EntityIndex,
                    PrefabType = (int) componentOverride.Item2.PrefabType,
                    SendTypeOptimization = (int) componentOverride.Item2.SendTypeOptimization,
                    ComponentVariant = componentOverride.Item2.VariantHash
                });
            }

            var bakingConfig = new GhostPrefabConfigBaking
            {
                Authoring = ghostAuthoring,
                Importance = ghostAuthoring.Importance,
                SupportedGhostModes = ghostAuthoring.SupportedGhostModes,
                DefaultGhostMode = ghostAuthoring.DefaultGhostMode,
                OptimizationMode = ghostAuthoring.OptimizationMode,
                UsePreSerialization = ghostAuthoring.UsePreSerialization
            };

            // Generate a ghost type component so the ghost can be identified by mathcing prefab asset guid
            var ghostType = GhostType.FromHash128String(ghostAuthoring.prefabId);
            var activeInScene = IsActive();

            AddComponent(entity, new GhostAuthoringComponentBakingData
            {
                GhostName = ghostName,
                GhostNameHash = ghostNameHash,
                BakingConfig = bakingConfig,
                GhostType = ghostType,
                Target = target,
                IsPrefab = isPrefab,
                IsActive = activeInScene
            });

            if (isPrefab)
            {
                AddComponent<GhostPrefabMetaData>(entity);
                if (target == NetcodeConversionTarget.ClientAndServer)
                    // Flag this prefab as needing runtime stripping
                    AddComponent<GhostPrefabRuntimeStrip>(entity);
            }

            if (isPrefab && (target != NetcodeConversionTarget.Server) && (bakingConfig.SupportedGhostModes != GhostModeMask.Interpolated))
                AddComponent<PredictedGhostSpawnRequest>(entity);
        }
    }

    // This type is used to mark the Ghost children and additional entities
    [BakingType]
    struct GhostChildEntityBaking : IComponentData
    {
        public Entity RootEntity;
    }

    // This type is used to mark the Ghost root
    [BakingType]
    struct GhostRootEntityBaking: IComponentData { }

    [UpdateInGroup(typeof(PostBakingSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [AlwaysSynchronizeSystem]
    [BakingVersion("cmarastoni", 1)]
    partial class GhostAuthoringBakingSystem : SystemBase
    {
        EntityQuery m_NoLongerBakedRootEntities;
        EntityQueryMask m_NoLongerBakedRootEntitiesMask;
        EntityQuery m_BakedEntitiesQuery;
        EntityQuery m_GhostEntities;
        EntityQueryMask m_BakedEntityMask;

        ComponentTypeSet m_ChildRevertBakingComponents = new ComponentTypeSet(new ComponentType[]
        {
            typeof(GhostChildEntity),
            typeof(GhostChildEntityBaking)
        });

        private ComponentTypeSet m_RootRevertBakingComponents = new ComponentTypeSet(new ComponentType[]
        {
            typeof(GhostType),
            typeof(GhostTypePartition),
            typeof(GhostInstance),
            typeof(PredictedGhost),
            typeof(PreSerializedGhost),
            typeof(SnapshotData),
            typeof(SnapshotDataBuffer),
            typeof(SnapshotDynamicDataBuffer),
            typeof(GhostRootEntityBaking),
        });

        protected override void OnCreate()
        {
            // Query to get all the root entities baked before
            m_NoLongerBakedRootEntities = GetEntityQuery(new EntityQueryDesc
            {
                None = new []
                {
                    ComponentType.ReadOnly<GhostAuthoringComponentBakingData>()
                },
                All = new []
                {
                    ComponentType.ReadOnly<GhostRootEntityBaking>()
                },
                Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab
            });
            Assert.IsFalse(m_NoLongerBakedRootEntities.HasFilter(), "EntityQueryMask will not respect the query's active filter settings.");
            m_NoLongerBakedRootEntitiesMask = m_NoLongerBakedRootEntities.GetEntityQueryMask();

            // Query to get all the root entities baked before
            m_GhostEntities = GetEntityQuery(new EntityQueryDesc
            {
                All = new []
                {
                    ComponentType.ReadOnly<GhostAuthoringComponentBakingData>()
                },
                Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab
            });


            EntityQueryDesc bakedDesc = new EntityQueryDesc()
            {
                All = new[] {ComponentType.FromTypeIndex(TypeManager.GetTypeIndex<BakedEntity>())},
                Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab
            };

            m_BakedEntitiesQuery = GetEntityQuery(bakedDesc);
            Assert.IsFalse(m_BakedEntitiesQuery.HasFilter(), "EntityQueryMask will not respect the query's active filter settings.");
            m_BakedEntityMask = m_BakedEntitiesQuery.GetEntityQueryMask();
        }

        void RevertPreviousBakings(NativeParallelHashSet<Entity> rootsToRebake)
        {
            // Revert all parents that were roots and they are not roots anymore
            EntityManager.RemoveComponent(m_NoLongerBakedRootEntities, m_RootRevertBakingComponents);

            // Revert all previously added components to the root, if the root is going to be recalculated, so incremental baking is consistent
            Entities
                .WithAll<GhostAuthoringComponentBakingData, GhostRootEntityBaking>()
                .ForEach((Entity rootEntity) =>
            {
                if (rootsToRebake.Contains(rootEntity))
                {
                    EntityManager.RemoveComponent(rootEntity, m_RootRevertBakingComponents);
                }
            }).WithEntityQueryOptions(EntityQueryOptions.IncludePrefab).WithStructuralChanges().Run();

            // Revert all previously added GhostChildEntity, to all the children that their root is going to be recalculated or not longer a root,
            // so incremental baking is consistent
            Entities
                .ForEach((Entity childEntity, in GhostChildEntityBaking child) =>
                {
                    if (rootsToRebake.Contains(child.RootEntity) || m_NoLongerBakedRootEntitiesMask.MatchesIgnoreFilter(child.RootEntity))
                    {
                        EntityManager.RemoveComponent(childEntity, m_ChildRevertBakingComponents);
                    }
                }).WithEntityQueryOptions(EntityQueryOptions.IncludePrefab).WithStructuralChanges().Run();
        }

        void AddRevertBakingTags(NativeArray<Entity> entities)
        {
            if (entities.Length > 0)
            {
                EntityManager.AddComponent<GhostRootEntityBaking>(entities[0]);

                var childComponent = new GhostChildEntityBaking { RootEntity = entities[0] };
                for (int index = 1; index < entities.Length; ++index)
                {
                    EntityManager.AddComponentData<GhostChildEntityBaking>(entities[index], childComponent);
                }
            }
        }

        NativeArray<Entity> GetEntityArrayFromLinkedEntityGroup(DynamicBuffer<LinkedEntityGroup> links)
        {
            unsafe
            {
                Debug.Assert(sizeof(LinkedEntityGroup) == sizeof(Entity));
            }
            var entityDynamicBuffer = links.Reinterpret<Entity>();
            var nativeArrayAlias = entityDynamicBuffer.AsNativeArray();

            var linkedEntities = new NativeArray<Entity>(links.Length, Allocator.Temp);
            NativeArray<Entity>.Copy(nativeArrayAlias, 0, linkedEntities, 0, nativeArrayAlias.Length);
            return linkedEntities;
        }

        protected override void OnUpdate()
        {
            var bakingSystem = World.GetExistingSystemManaged<BakingSystem>();

            int ghostCount = m_GhostEntities.CalculateEntityCount();
            NativeParallelHashSet<Entity> rootsToProcess = new NativeParallelHashSet<Entity>(ghostCount, Allocator.TempJob);
            var rootsToProcessWriter = rootsToProcess.AsParallelWriter();
            var bakedMask = m_BakedEntityMask;

            //ATTENTION! This singleton entity is always destroyed in the first non-incremental pass, because in the first import
            //the baking system clean all the Entities in the world when you open a sub-scene.
            //We recreate the entity here "lazily", so everything behave as expected.
            if (!SystemAPI.TryGetSingleton<GhostComponentSerializerCollectionData>(out var serializerCollectionData))
            {
                var systemGroup = World.GetExistingSystemManaged<GhostComponentSerializerCollectionSystemGroup>();
                EntityManager.CreateSingleton(systemGroup.ghostComponentSerializerCollectionDataCache);
                serializerCollectionData = systemGroup.ghostComponentSerializerCollectionDataCache;
            }

            // This code is selecting from all the roots, the ones that have been baked themselves or the ones where at least one child has been baked.
            // The component BakedEntity is a TemporaryBakingType that is added to every entity that has baked on this baking pass.
            // In bakers we can create a dependency on an object's data, but we can't create a dependency on an object baking.
            // So if a child depends on some data, when that data changes the child will bake, but the root has no way of knowing it has to be processed again as the child itself hasn't changed.
            Entities
                .WithAll<GhostAuthoringComponentBakingData>()
                .ForEach((Entity rootEntity, DynamicBuffer<LinkedEntityGroup> linkedEntityGroup) =>
                {
                    foreach (var child in linkedEntityGroup)
                    {
                        if (bakedMask.MatchesIgnoreFilter(child.Value))
                        {
                            rootsToProcessWriter.Add(rootEntity);
                            break;
                        }
                    }
                }).WithEntityQueryOptions(EntityQueryOptions.IncludePrefab).WithNativeDisableParallelForRestriction(rootsToProcessWriter)
                .ScheduleParallel(default).Complete();

            // Revert the previously added components
            RevertPreviousBakings(rootsToProcess);

            var context = new BlobAssetComputationContext<int, GhostPrefabBlobMetaData>(bakingSystem.BlobAssetStore, 16, Allocator.Temp);
            Entities.ForEach((Entity rootEntity, DynamicBuffer<LinkedEntityGroup> linkedEntityGroup, in GhostAuthoringComponentBakingData ghostAuthoringBakingData) =>
            {
                if (!rootsToProcess.Contains(rootEntity))
                    return;

                NativeArray<Entity> linkedEntities = GetEntityArrayFromLinkedEntityGroup(linkedEntityGroup);
                // Mark the entities so they can be reverted later on subsequent baking
                AddRevertBakingTags(linkedEntities);

                GhostPrefabCreation.CollectAllComponents(EntityManager, linkedEntities,
                    out var allComponents, out var componentCounts);

                //PrefabTypes is not part of the ghost metadata blob. But it is computed and stored in this array
                //to simplify the subsequent logics. This value depend on serialization variant selected for the type
                var prefabTypes = new NativeArray<GhostPrefabType>(allComponents.Length, Allocator.Temp);
                var sendMasksOverride = new NativeArray<int>(allComponents.Length, Allocator.Temp);
                var variants = new NativeArray<ulong>(allComponents.Length, Allocator.Temp);

                // Setup all components GhostType, variants, sendMask and sendToChild arrays. Used later to mark components to be added or removed.
                DynamicBuffer<GhostAuthoringComponentOverridesBaking> overrides = EntityManager.GetBuffer<GhostAuthoringComponentOverridesBaking>(rootEntity);
                var compIdx = 0;
                for (int k = 0; k < linkedEntities.Length; ++k)
                {
                    var isChild = k != 0;
                    var entityGUID = EntityManager.GetComponentData<EntityGuid>(linkedEntities[k]);
                    var instanceId = entityGUID.OriginatingId;
                    var numComponents = componentCounts[k];
                    for (int i = 0; i < numComponents; ++i, ++compIdx)
                    {
                        // Find the override
                        GhostAuthoringComponentOverridesBaking? myOverride = default;
                        foreach (var overrideEntry in overrides)
                        {
                            ulong fullTypeNameID = TypeManager.GetFullNameHash(allComponents[compIdx].TypeIndex);
                            if (overrideEntry.FullTypeNameID == fullTypeNameID && overrideEntry.GameObjectID == instanceId)
                            {
                                myOverride = overrideEntry;
                                break;
                            }
                        }

                        //Initialize the value with common default and they overwrite them in case is necessary.
                        prefabTypes[compIdx] = GhostPrefabType.All;
                        ulong variantHash = myOverride.HasValue ? myOverride.Value.ComponentVariant : 0;
                        bool isRoot = !isChild;
                        var variantType = serializerCollectionData.GetCurrentSerializationStrategyForComponent(allComponents[compIdx], variantHash, isRoot);
                        variants[compIdx] = variantType.Hash;
                        sendMasksOverride[compIdx] = GhostAuthoringInspectionComponent.ComponentOverride.NoOverride;

                        // NW: Disabled warning while investigating CI timeout error on mac: [TimeoutExceptionMessage]: Timeout while waiting for a log message, no editor logging has happened during the timeout window
                        //if (variantType.IsTestVariant != 0)
                        //{
                        //    Debug.LogWarning($"Ghost '{ghostAuthoringBakingData.GhostName}' uses a test variant {variantType.ToFixedString()}! Ensure this is only ever used in an Editor, test context.");
                        //}

                        //Initialize the common default and then overwrite in case
                        if (myOverride.HasValue)
                        {
                            if (myOverride.Value.ComponentVariant != 0) // Not an error if the hash is 0 (default).
                            {
                                if (variantType.Hash != myOverride.Value.ComponentVariant)
                                {
                                    Debug.LogError($"Ghost '{ghostAuthoringBakingData.GhostName}' has an override for type {allComponents[compIdx].ToFixedString()} that sets the Variant to hash '{myOverride.Value.ComponentVariant}'. However, this hash is no longer present in code-gen, likely due to a code change removing or renaming the old variant. Thus, using Variant '{variantType.DisplayName}' (with hash: '{variantType.Hash}') and ignoring your \"Component Override\". Please open this prefab and re-apply.");
                                }
                            }

                            //Only override the the default if the property is meant to (so always check for UseDefaultValue first)
                            if (myOverride.Value.PrefabType != GhostAuthoringInspectionComponent.ComponentOverride.NoOverride)
                                prefabTypes[compIdx] = (GhostPrefabType) myOverride.Value.PrefabType;
                            else
                                // Problem: if the variant attribute changed, or we removed a variant,
                                // subscenes and prefabs aren't reconverted (they are not in the subscene or components).
                                // Unless we enforce only runtime stripping, checking what variant you expect at conversion
                                // is mandatory.
                                prefabTypes[compIdx] = variantType.PrefabType;
                            if (myOverride.Value.SendTypeOptimization != GhostAuthoringInspectionComponent.ComponentOverride.NoOverride)
                                sendMasksOverride[compIdx] = myOverride.Value.SendTypeOptimization;
                        }
                        else
                            prefabTypes[compIdx] = variantType.PrefabType;
                    }
                }

                GhostPrefabCreation.Config config = new GhostPrefabCreation.Config
                {
                    Name = ghostAuthoringBakingData.GhostName,
                    Importance = ghostAuthoringBakingData.BakingConfig.Importance,
                    SupportedGhostModes = ghostAuthoringBakingData.BakingConfig.SupportedGhostModes,
                    DefaultGhostMode = ghostAuthoringBakingData.BakingConfig.DefaultGhostMode,
                    OptimizationMode = ghostAuthoringBakingData.BakingConfig.OptimizationMode,
                    UsePreSerialization = ghostAuthoringBakingData.BakingConfig.UsePreSerialization
                };

                GhostPrefabCreation.FinalizePrefabComponents(config, EntityManager,
                    rootEntity, ghostAuthoringBakingData.GhostType, linkedEntities,
                    allComponents, componentCounts, ghostAuthoringBakingData.Target, prefabTypes);

                if (ghostAuthoringBakingData.IsPrefab)
                {
                    var contentHash = TypeHash.FNV1A64(ghostAuthoringBakingData.BakingConfig.Importance);
                    contentHash = TypeHash.CombineFNV1A64(contentHash,
                        TypeHash.FNV1A64((int) ghostAuthoringBakingData.BakingConfig.SupportedGhostModes));
                    contentHash = TypeHash.CombineFNV1A64(contentHash,
                        TypeHash.FNV1A64((int) ghostAuthoringBakingData.BakingConfig.DefaultGhostMode));
                    contentHash = TypeHash.CombineFNV1A64(contentHash,
                        TypeHash.FNV1A64((int) ghostAuthoringBakingData.BakingConfig.OptimizationMode));
                    contentHash = TypeHash.CombineFNV1A64(contentHash, ghostAuthoringBakingData.GhostNameHash);
                    for (int i = 0; i < componentCounts[0]; ++i)
                    {
                        var comp = allComponents[i];
                        var prefabType = prefabTypes[i];
                        contentHash = TypeHash.CombineFNV1A64(contentHash,
                            TypeManager.GetTypeInfo(comp.TypeIndex).StableTypeHash);
                        contentHash = TypeHash.CombineFNV1A64(contentHash, TypeHash.FNV1A64((int) prefabType));
                    }

                    compIdx = componentCounts[0];
                    for (int i = 1; i < linkedEntities.Length; ++i)
                    {
                        contentHash = TypeHash.CombineFNV1A64(contentHash, TypeHash.FNV1A64(i));
                        var numComponent = componentCounts[i];
                        for (int k = 0; k < numComponent; ++k, ++compIdx)
                        {
                            var comp = allComponents[compIdx];
                            var prefabType = prefabTypes[compIdx];
                            contentHash = TypeHash.CombineFNV1A64(contentHash,
                                TypeManager.GetTypeInfo(comp.TypeIndex).StableTypeHash);
                            contentHash = TypeHash.CombineFNV1A64(contentHash, TypeHash.FNV1A64((int) prefabType));
                        }
                    }

                    var blobHash = new Unity.Entities.Hash128(
                        ghostAuthoringBakingData.GhostType.guid0 ^ (uint) (contentHash >> 32),
                        ghostAuthoringBakingData.GhostType.guid1 ^ (uint) (contentHash),
                        ghostAuthoringBakingData.GhostType.guid2, ghostAuthoringBakingData.GhostType.guid3);
                    // instanceIds[0] contains the root GameObject instance id
                    if (context.NeedToComputeBlobAsset(blobHash))
                    {
                        var blobAsset = GhostPrefabCreation.CreateBlobAsset(config,
                            EntityManager, rootEntity, linkedEntities,
                            allComponents, componentCounts, ghostAuthoringBakingData.Target, prefabTypes,
                            sendMasksOverride, variants);
                        context.AddComputedBlobAsset(blobHash, blobAsset);
                    }

                    context.GetBlobAsset(blobHash, out var blob);
                    EntityManager.SetComponentData(rootEntity, new GhostPrefabMetaData {Value = blob});
                }
            }).WithStructuralChanges().WithoutBurst().WithEntityQueryOptions(EntityQueryOptions.IncludePrefab).Run();
            rootsToProcess.Dispose();
        }
    }
}
