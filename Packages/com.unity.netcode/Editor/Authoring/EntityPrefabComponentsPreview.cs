using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using Unity.Collections.NotBurstCompatible;
using Unity.Entities;
using Unity.Entities.Conversion;
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
                x.GetManagedType().FullName.CompareTo(y.GetManagedType().FullName);
        }

        /// <summary>Triggers the baking conversion process on the 'authoringComponent' and appends all resulting baked entities and components to the 'bakedDataMap'.</summary>
        public void BakeEntireNetcodePrefab(GhostAuthoringComponent authoringComponent, Dictionary<GameObject, BakedGameObjectResult> bakedDataMap)
        {
            try
            {
                EditorUtility.DisplayProgressBar($"Baking '{authoringComponent}'...", "Baking triggered by the GhostAuthoringInspectionComponent.", .9f);
                GhostAuthoringInspectionComponent.forceBake = false;
                GhostAuthoringInspectionComponent.forceSave = true;

                // TODO - Handle exceptions due to invalid prefab setup. E.g.
                // "InvalidOperationException: OwnerPrediction mode can only be used on prefabs which have a GhostOwner"
                using(var world = new World(nameof(EntityPrefabComponentsPreview)))
                {
                    using var blobAssetStore = new BlobAssetStore(128);
                    authoringComponent.ForcePrefabConversion = true;

                    var bakingSettings = new BakingSettings(BakingUtility.BakingFlags.AddEntityGUID, blobAssetStore);
                    BakingUtility.BakeGameObjects(world, new[] {authoringComponent.gameObject}, bakingSettings);
                    var bakingSystem = world.GetExistingSystemManaged<BakingSystem>();
                    var primaryEntitiesMap = new HashSet<Entity>(16);

                    var primaryEntity = bakingSystem.GetEntity(authoringComponent.gameObject);
                    var ghostBlobAsset = world.EntityManager.GetComponentData<GhostPrefabMetaData>(primaryEntity).Value;

                    CreatedBakedResultForPrimaryEntities(world, bakedDataMap, authoringComponent, bakingSystem, primaryEntitiesMap, ghostBlobAsset);
                    CreatedBakedResultForLinkedEntities(world, bakedDataMap, primaryEntitiesMap, ghostBlobAsset);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                authoringComponent.ForcePrefabConversion = false;
            }
        }

        void CreatedBakedResultForPrimaryEntities(World world, Dictionary<GameObject, BakedGameObjectResult> bakedDataMap, GhostAuthoringComponent authoringComponent, BakingSystem bakingSystem, HashSet<Entity> primaryEntitiesMap, BlobAssetReference<GhostPrefabBlobMetaData> blobAssetReference)
        {
            foreach (var t in authoringComponent.GetComponentsInChildren<Transform>())
            {
                var go = t.gameObject;

                // I'd like to skip children that DONT have an Inspection component, but not possible as they may add one.

                var sourcePrefabPath = AssetDatabase.GetAssetPath(go);
                var result = new BakedGameObjectResult
                {
                    SourceGameObject = go,
                    SourcePrefabPath = sourcePrefabPath,
                    RootAuthoring = authoringComponent,
                    BakedEntities = new List<BakedEntityResult>(1)
                };

                var primaryEntity = bakingSystem.GetEntity(go);
                if (bakingSystem.EntityManager.Exists(primaryEntity))
                {
                    result.BakedEntities.Add(CreateBakedEntityResult(result, 0, world, primaryEntity, false, blobAssetReference));
                    primaryEntitiesMap.Add(primaryEntity);
                }
                bakedDataMap[go] = result;
            }
        }

        void CreatedBakedResultForLinkedEntities(World world, Dictionary<GameObject, BakedGameObjectResult> bakedDataMap, HashSet<Entity> primaryEntitiesMap, BlobAssetReference<GhostPrefabBlobMetaData> blobAssetReference)
        {
            foreach (var kvp in bakedDataMap)
            {
                // TODO - Test-case to ensure the root entity does not contain ALL linked entities (even for children + additional).
                for (int index = 0, max = kvp.Value.BakedEntities.Count; index < max; index++)
                {
                    var bakedEntityResult = kvp.Value.BakedEntities[index];
                    var primaryEntity = bakedEntityResult.Entity;
                    if (world.EntityManager.HasComponent<LinkedEntityGroup>(primaryEntity))
                    {
                        var linkedEntityGroup = world.EntityManager.GetBuffer<LinkedEntityGroup>(primaryEntity);
                        for (int i = 1; i < linkedEntityGroup.Length; ++i)
                        {
                            var linkedEntity = linkedEntityGroup[i].Value;

                            // Only show linked entities if they're not primary entities of child GameObjects.
                            // I.e. Only possible if, during Baking, users call `CreateAdditionalEntity`.
                            if (!primaryEntitiesMap.Contains(linkedEntity))
                            {
                                kvp.Value.BakedEntities.Add(CreateBakedEntityResult(kvp.Value, i, world, linkedEntity, true, blobAssetReference));
                            }
                        }
                    }
                }
            }
        }

        BakedEntityResult CreateBakedEntityResult(BakedGameObjectResult parent, int entityIndex, World world, Entity convertedEntity, bool isLinkedEntity, BlobAssetReference<GhostPrefabBlobMetaData> blobAssetReference)
        {
            var isRoot = parent.SourceGameObject == parent.RootAuthoring.gameObject;
            var guid = world.EntityManager.GetComponentData<EntityGuid>(convertedEntity);
            var result = new BakedEntityResult
            {
                GoParent = parent,
                Entity = convertedEntity,
                Guid = guid,
                EntityName = world.EntityManager.GetName(convertedEntity),
                EntityIndex = entityIndex,
                BakedComponents = new List<BakedComponentItem>(16),
                IsLinkedEntity = isLinkedEntity,
                IsRoot = isRoot,
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
                compItem.serializationStrategy = collectionData.SelectSerializationStrategyForComponentWithHash(ComponentType.ReadWrite(compItem.managedType), searchHash, variantTypesList, isRoot);
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
