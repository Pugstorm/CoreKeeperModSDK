using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.NotBurstCompatible;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace Unity.NetCode.Editor
{
    /// <summary>
    /// Extract from the prefab the converted entities components, in respect to the selected variant and default
    /// mapping provided by the user
    /// </summary>
    class EntityPrefabComponentsPreview
    {
        struct ComponentNameComparer : IComparer<ComponentType>
        {
            public int Compare(ComponentType x, ComponentType y) =>
                string.Compare(x.GetManagedType().FullName, y.GetManagedType().FullName, StringComparison.Ordinal);
        }

        /// <summary>Triggers the baking conversion process on the 'authoringComponent' and appends all resulting baked entities and components to the 'bakedDataMap'.</summary>
        public void BakeEntireNetcodePrefab(GhostAuthoringComponent ghostAuthoring, GhostAuthoringInspectionComponent inspectionComponent, Dictionary<GhostAuthoringInspectionComponent, BakedResult> cachedBakedResults)
        {
            GhostAuthoringInspectionComponent.forceBake = false;
            if (ghostAuthoring == null)
            {
                Debug.LogError($"Attempting to bake `GhostAuthoringInspectionComponent` '{inspectionComponent.name}', but no root `GhostAuthoringComponent` found!");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar($"Baking '{ghostAuthoring}'...", "Baking triggered by the GhostAuthoringInspectionComponent.", .9f);

                // TODO - Handle exceptions due to invalid prefab setup. E.g.
                // "InvalidOperationException: OwnerPrediction mode can only be used on prefabs which have a GhostOwner"
                using var world = new World(nameof(EntityPrefabComponentsPreview));
                using var blobAssetStore = new BlobAssetStore(128);
                ghostAuthoring.ForcePrefabConversion = true;

                var bakeResult = new BakedResult
                {
                    GhostAuthoring = ghostAuthoring,
                    GameObjectResults = new (32),
                };

                var bakingSettings = new BakingSettings(BakingUtility.BakingFlags.AddEntityGUID, blobAssetStore);
                BakingUtility.BakeGameObjects(world, new[] {ghostAuthoring.gameObject}, bakingSettings);
                var bakingSystem = world.GetExistingSystemManaged<BakingSystem>();
                var primaryEntitiesMap = new HashSet<Entity>(16);

                var primaryEntity = bakingSystem.GetEntity(ghostAuthoring.gameObject);
                var ghostBlobAsset = world.EntityManager.GetComponentData<GhostPrefabMetaData>(primaryEntity).Value;

                CreatedBakedResultForPrimaryEntities(bakeResult, world, bakingSystem, primaryEntitiesMap, ghostBlobAsset, cachedBakedResults);
                CreatedBakedResultForAdditionalEntities(bakeResult, world, primaryEntitiesMap, ghostBlobAsset, bakingSystem);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                GhostAuthoringInspectionComponent.forceRebuildInspector = true;
                ghostAuthoring.ForcePrefabConversion = false;
            }
        }


        internal static int CountComponents(GameObject go)
        {
            return go.GetComponents<Component>().Length;
        }

        static void CreatedBakedResultForPrimaryEntities(BakedResult bakedResult, World world, BakingSystem bakingSystem, HashSet<Entity> primaryEntitiesMap, BlobAssetReference<GhostPrefabBlobMetaData> blobAssetReference, Dictionary<GhostAuthoringInspectionComponent, BakedResult> cachedBakedResults)
        {
            foreach (var t in bakedResult.GhostAuthoring.GetComponentsInChildren<Transform>())
            {
                var go = t.gameObject;

                var sourcePrefabPath = AssetDatabase.GetAssetPath(go);
                var goResult = new BakedGameObjectResult
                {
                    AuthoringRoot = bakedResult,
                    SourceGameObject = go,
                    SourceInspection = go.GetComponent<GhostAuthoringInspectionComponent>(),
                    SourcePrefabPath = sourcePrefabPath,
                    BakedEntities = new List<BakedEntityResult>(2),
                    NumComponents = CountComponents(go),
                };
                var discoveredInspectionComponent = goResult.SourceInspection;
                if (discoveredInspectionComponent != null)
                    cachedBakedResults[discoveredInspectionComponent] = bakedResult;

                var primaryEntity = bakingSystem.GetEntity(go);
                if (bakingSystem.EntityManager.Exists(primaryEntity))
                {
                    goResult.BakedEntities.Add(CreateBakedEntityResult(goResult, 0, world, bakingSystem, primaryEntity, false, blobAssetReference));
                    primaryEntitiesMap.Add(primaryEntity);
                }
                bakedResult.GameObjectResults[go] = goResult;
            }
        }

        static void CreatedBakedResultForAdditionalEntities(BakedResult bakedResult, World world, HashSet<Entity> primaryEntitiesMap, BlobAssetReference<GhostPrefabBlobMetaData> blobAssetReference, BakingSystem bakingSystem)
        {
            // Note: We only expect the ROOT entity to have a LinkedEntityGroup,
            // but checking EVERY baked GameObject as this is not an assumption we control.
            foreach (var kvp in bakedResult.GameObjectResults)
            {
                // TODO - Test-case to ensure the root entity does not contain ALL linked entities (even for children + additional).
                for (int index = 0, max = kvp.Value.BakedEntities.Count; index < max; index++)
                {
                    var bakedEntityResult = kvp.Value.BakedEntities[index];
                    var primaryEntity = bakedEntityResult.Entity;
                    if (!world.EntityManager.HasComponent<LinkedEntityGroup>(primaryEntity))
                        continue;

                    var linkedEntityGroup = world.EntityManager.GetBuffer<LinkedEntityGroup>(primaryEntity);
                    for (int i = 1; i < linkedEntityGroup.Length; ++i)
                    {
                        var linkedEntity = linkedEntityGroup[i].Value;

                        // Child entities are considered 'primary' entities. Thus, ignore them.
                        // I.e. During Baking, if users call `CreateAdditionalEntity`, it won't be 'primary'.
                        if (primaryEntitiesMap.Contains(linkedEntity))
                            continue;

                        // Find the actual authoring GameObject for this linked entity. It might be one of our children.
                        var foundActualAuthoring = TryGetAuthoringForAdditionalEntity(linkedEntity, bakingSystem, bakedResult.GameObjectResults.Values, out var actualAuthoring);
                        if (!foundActualAuthoring)
                        {
                            Debug.LogWarning($"Expected to find the source BakedGameObjectResult for Additional Entity '{linkedEntity.ToFixedString()}' ('{bakingSystem.EntityManager.GetName(linkedEntity)}') (via EntityGuid search), but did not! Assuming the authoring GameObject is '{kvp.Value.SourceGameObject.name}'! Please file a bug report if this assumption is false.", kvp.Value.SourceGameObject);

                            actualAuthoring = kvp.Value;
                        }
                        var entityResult = CreateBakedEntityResult(actualAuthoring, i, world, bakingSystem, linkedEntity, true, blobAssetReference);
                        actualAuthoring.BakedEntities.Add(entityResult);
                    }
                }
            }
        }

        static bool TryGetAuthoringForAdditionalEntity(Entity additionalEntity, BakingSystem bakingSystem, Dictionary<GameObject, BakedGameObjectResult>.ValueCollection results, out BakedGameObjectResult found)
        {
            found = default;
            if (!bakingSystem.EntityManager.HasComponent<EntityGuid>(additionalEntity))
            {
                Debug.LogError($"Additional entity '{additionalEntity.ToFixedString()}' did not have an EntityGuid! Thus, cannot find Authoring for it!");
                return false;
            }
            var additionalEntitiesEntityGuid = bakingSystem.EntityManager.GetComponentData<EntityGuid>(additionalEntity);

            foreach (var result in results)
            {
                foreach (var x in result.BakedEntities)
                {
                    if (x.Guid.OriginatingId == additionalEntitiesEntityGuid.OriginatingId)
                    {
                        found = result;
                        return true;
                    }
                }
            }

            return false;
        }

        static BakedEntityResult CreateBakedEntityResult(BakedGameObjectResult authoring, int entityIndex, World world, BakingSystem bakingSystem, Entity convertedEntity, bool isLinkedEntity, BlobAssetReference<GhostPrefabBlobMetaData> blobAssetReference)
        {
            var guid = world.EntityManager.GetComponentData<EntityGuid>(convertedEntity);
            var result = new BakedEntityResult
            {
                GoParent = authoring,
                Entity = convertedEntity,
                Guid = guid,
                EntityName = world.EntityManager.GetName(convertedEntity),
                EntityIndex = entityIndex,
                BakedComponents = new List<BakedComponentItem>(16),
                IsLinkedEntity = isLinkedEntity,
            };

            using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostComponentSerializerCollectionData>());
            var collectionData = query.GetSingleton<GhostComponentSerializerCollectionData>();

            AddToComponentList(result, result.BakedComponents, collectionData, world, convertedEntity, entityIndex, blobAssetReference);

            var variantTypesList = new NativeList<ComponentTypeSerializationStrategy>(4, Allocator.Temp);
            foreach (var compItem in result.BakedComponents)
            {
                var searchHash = compItem.VariantHash;

                variantTypesList.Clear();
                for (int i = 0; i < compItem.availableSerializationStrategies.Length; i++)
                {
                    variantTypesList.Add(compItem.availableSerializationStrategies[i]);
                }
                compItem.serializationStrategy = collectionData.SelectSerializationStrategyForComponentWithHash(ComponentType.ReadWrite(compItem.managedType), searchHash, variantTypesList, result.IsRoot);
                compItem.sendToOwnerType = compItem.serializationStrategy.IsSerialized != 0 ? collectionData.Serializers[compItem.serializationStrategy.SerializerIndex].SendToOwner : SendToOwnerType.None;

                if (compItem.anyVariantIsSerialized)
                {
                    compItem.SaveVariant(true, false);
                }
                else
                {
                    if (compItem.VariantHash != 0)
                    {
                        Debug.LogWarning($"`{compItem.fullname}` has Variant Hash '{compItem.VariantHash}' but this type is not a GhostComponent. Removing Variant!");
                        compItem.ResetVariantToDefault();
                    }
                }
            }
            variantTypesList.Dispose();
            return result;
        }

        static void AddToComponentList(BakedEntityResult parent, List<BakedComponentItem> newComponents, GhostComponentSerializerCollectionData collectionData, World world, Entity convertedEntity, int entityIndex, BlobAssetReference<GhostPrefabBlobMetaData> blobAssetReference)
        {
            var compTypes = world.EntityManager.GetComponentTypes(convertedEntity);
            compTypes.Sort(default(ComponentNameComparer));

            // Store all types:
            for (int i = 0; i < compTypes.Length; ++i)
                CreateBakedComponentItem(compTypes[i]);

            // Store the types that have been removed from BOTH the server and client (as they'd not be found via the above):
            TryAddRemoved(ref blobAssetReference.Value.RemoveOnServer);
            TryAddRemoved(ref blobAssetReference.Value.RemoveOnClient);

            void TryAddRemoved(ref BlobArray<GhostPrefabBlobMetaData.ComponentReference> removedArray)
            {
                for (var i = 0; i < removedArray.Length; i++)
                {
                    var removedCompRef = removedArray[i];
                    if (removedCompRef.EntityIndex != entityIndex) continue;
                    var removedComp = ComponentType.FromTypeIndex(TypeManager.GetTypeIndexFromStableTypeHash(removedCompRef.StableHash));
                    bool IsNotAlreadyAdded(BakedComponentItem x) => x.managedType != removedComp.GetManagedType();
                    if (newComponents.All(IsNotAlreadyAdded))
                        CreateBakedComponentItem(removedComp);
                }
            }

            void CreateBakedComponentItem(ComponentType componentType)
            {
                var managedType = componentType.GetManagedType();
                if (managedType == typeof(Prefab) || managedType == typeof(LinkedEntityGroup))
                    return;

                var componentItem = new BakedComponentItem
                {
                    EntityParent = parent,
                    fullname = managedType.FullName,
                    managedType = managedType,
                    entityIndex = entityIndex,
                };

                using var availableSs = collectionData.GetAllAvailableSerializationStrategiesForType(managedType, componentItem.VariantHash, parent.IsRoot);
                var canSerializeInAtLeastOneVariant = GhostComponentSerializerCollectionData.AnyVariantsAreSerialized(in availableSs);
                var defaultVariant = collectionData.GetCurrentSerializationStrategyForComponent(managedType, 0, parent.IsRoot);

                // Remove test variants as they cannot be selected:
                for (var j = availableSs.Length - 1; j >= 0; j--)
                {
                    var ss = availableSs[j];
                    if (ss.IsTestVariant != 0)
                        availableSs.RemoveAt(j);
                }

                // Cache the availableVariants names.
                var ssDisplayNames = new string[availableSs.Length];
                for (var j = 0; j < availableSs.Length; j++)
                {
                    var vt = availableSs[j];
                    ssDisplayNames[j] = vt.DisplayName.ToString();
                    if (defaultVariant.Hash == availableSs[j].Hash)
                        ssDisplayNames[j] += $" ({ComponentTypeSerializationStrategy.GetDefaultDisplayName(defaultVariant.DefaultRule)})";
                }

                componentItem.availableSerializationStrategies = availableSs.ToArrayNBC();
                componentItem.availableSerializationStrategyDisplayNames = ssDisplayNames;
                componentItem.anyVariantIsSerialized = canSerializeInAtLeastOneVariant;
                componentItem.defaultSerializationStrategy = defaultVariant;
                newComponents.Add(componentItem);
            }
        }
    }
}
