using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Transforms;
using UnityEngine;

namespace Unity.NetCode
{
    /// <summary>
    /// Component added during the baking process to signal that this pre-spawned ghost has been baked.
    /// </summary>
    [BakingType]
    internal struct PrespawnedGhostBakedBefore: IComponentData { }

    /// <summary>
    /// Postprocess all the game objects present in a subscene which present a GhostAuthoringComponent by adding to the primary
    /// entities the following components:
    /// - A PreSpawnedGhostIndex component: contains a unique identifier (per subscene) that is guaranteed to be deterministic
    /// - A SubSceneGhostComponentHash shared component: used to deterministically group the ghost instances
    /// </summary>
    ///
    [UpdateInGroup(typeof(PostBakingSystemGroup))]
    [UpdateAfter(typeof(GhostAuthoringBakingSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [BakingVersion("cmarastoni", 1)]
    partial class PreSpawnedGhostsBakingSystem : SystemBase
    {
        private EntityQuery m_SceneSectionEntityQuery;

        protected override void OnDestroy()
        {
            if (EntityManager.IsQueryValid(m_SceneSectionEntityQuery))
                m_SceneSectionEntityQuery.Dispose();
        }

        protected override void OnUpdate()
        {
            var hashToEntity = new NativeParallelHashMap<ulong, Entity>(128, Allocator.TempJob);

            // TODO: Check that the GhostAuthoringComponent is interpolated, as we don't support predicted atm
            Entities.ForEach((Entity entity, in GhostAuthoringComponentBakingData ghostAuthoringBakingData) =>
            {
                var isInSubscene = EntityManager.HasComponent<SceneSection>(entity);
                bool isPrefab = ghostAuthoringBakingData.IsPrefab;
                var activeInScene = ghostAuthoringBakingData.IsActive;
                if (!isPrefab && isInSubscene && activeInScene)
                {
                    var hashData = new NativeList<ulong>(Allocator.Temp);
					//We are using the ghost type to identify the ghost archetype. It is the only reliable value
                    //in between server and client. Baking can add/remove component on the entity based on the conversion
                    //target. So using archetype.StableHash does not work in our case.
                    hashData.Add(ghostAuthoringBakingData.GhostType.guid0);
                    hashData.Add(ghostAuthoringBakingData.GhostType.guid1);
                    hashData.Add(ghostAuthoringBakingData.GhostType.guid2);
                    hashData.Add(ghostAuthoringBakingData.GhostType.guid3);

					//What happen if the entity has been authored such that the position and rotation are not present?
                    //We are relying on the TransformAuthoring instead, to have stable data that depend only on the gameobject
                    //authoring
                    var transformAuthoring = EntityManager.GetComponentData<TransformAuthoring>(entity);

                    unsafe
                    {
                        var positionData = (byte*)&transformAuthoring.Position;
                        var rotationData = (byte*)&transformAuthoring.Rotation;
                        hashData.Add(Unity.Core.XXHash.Hash64(positionData, 3*sizeof(float)));
                        hashData.Add(Unity.Core.XXHash.Hash64(rotationData, 4*sizeof(float)));
                    }
                    // More components could be added here to get a better hash result (and support identical position/rotation)
                    // but care needs to be taken as to only include components guaranteed to exist on the entity in general
                    // and also on both client and server. This just covers the safest route of taking only position/rotation.

                    //Add the scene guid at the very end. This is to seed the scene-hash based also on the baked scene section.
                    var sceneSection = EntityManager.GetSharedComponent<SceneSection>(entity);
                    hashData.Add(sceneSection.SceneGUID.Value[0]);
                    hashData.Add(sceneSection.SceneGUID.Value[1]);
                    hashData.Add(sceneSection.SceneGUID.Value[2]);
                    hashData.Add(sceneSection.SceneGUID.Value[3]);
                    ulong combinedComponentHash;
                    unsafe
                    {
                        combinedComponentHash = Unity.Core.XXHash.Hash64((byte*) hashData.GetUnsafeReadOnlyPtr(),
                            hashData.Length * sizeof(ulong));
                    }

                    // When duplicating a scene object it will have the same position/rotation as the original, so until that
                    // changes there will always be a duplicate hash until it's moved to it's own location
                    if (!hashToEntity.ContainsKey(combinedComponentHash))
                        hashToEntity.Add(combinedComponentHash, entity);
                    else
                        Debug.LogError($"Two ghosts can't be in the same exact position and rotation {EntityManager.GetName(entity)}");
                }
            }).WithoutBurst().Run();

            if (hashToEntity.Count() > 0)
            {
                //Add the components in batch
                var values = hashToEntity.GetValueArray(Allocator.Temp);
                EntityManager.AddComponent(values, typeof(PreSpawnedGhostIndex));
                EntityManager.AddComponent(values, typeof(PrespawnGhostBaseline));
                EntityManager.AddComponent(values, typeof(PrespawnedGhostBakedBefore));

                var keys = hashToEntity.GetKeyArray(Allocator.Temp);
                keys.Sort();

                // Assign ghost IDs to the pre-spawned entities sorted by component data hash
                for (int i = 0; i < keys.Length; ++i)
                {
                    EntityManager.SetComponentData(hashToEntity[keys[i]], new PreSpawnedGhostIndex {Value = i});
                    //We need to pre-assign the ghostType to -1 so that that the ghost is actually identified as prespawn
                    //befor
                    EntityManager.SetComponentData(hashToEntity[keys[i]], new GhostInstance
                    {
                        ghostId = 0,
                        // GhostType -1 is a special case for prespawned ghosts which is converted to a proper ghost id in the send / receive systems
                        // once the ghost ids are known
                        ghostType = -1,
                        spawnTick = NetworkTick.Invalid
                    });

                    //Disable the entity so the ghost cannot be retrieved before the prespawn baseline are calculated
                    EntityManager.AddComponent<Disabled>(hashToEntity[keys[i]]);
                }

                // Save the final subscene hash with all the pre-spawned ghosts
                ulong hash;
                unsafe
                {
                    hash = Unity.Core.XXHash.Hash64((byte*) keys.GetUnsafeReadOnlyPtr(),
                        keys.Length * sizeof(ulong));
                }

                for (int i = 0; i < keys.Length; ++i)
                {
                    // Track the subscene which is the parent of this entity
                    EntityManager.AddSharedComponent(hashToEntity[keys[i]], new SubSceneGhostComponentHash {Value = hash});
                }


                //Add the SubSceneWithPrespawnGhosts to the scene entity
                //FIXME: current limitation: we are expecting all the prespawn entities belonging to same section
                var sectionEntity = GetSceneSectionEntity(hashToEntity[keys[0]]);
                if (sectionEntity != Entity.Null)
                {
                    EntityManager.AddComponentData(sectionEntity, new SubSceneWithPrespawnGhosts
                    {
                        SubSceneHash = hash,
                        PrespawnCount = keys.Length
                    });
                    EntityManager.AddComponent<PrespawnedGhostBakedBefore>(sectionEntity);
                }
                //We can add more here. Ideally the serialization. A way would be to use a sort of offset re-mapping
            }

            hashToEntity.Dispose();
        }

        public Entity GetSceneSectionEntity(Entity entity)
        {
            var sceneSection = EntityManager.GetSharedComponent<SceneSection>(entity);
            return SerializeUtility.GetSceneSectionEntity(sceneSection.Section, EntityManager, ref m_SceneSectionEntityQuery);
        }
    }

    /// <summary>
    /// Clean up all the components added by PreSpawnedGhostsBakingSystem in previous baking passes
    /// </summary>
    ///
    [UpdateInGroup(typeof(PreBakingSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [BakingVersion("cmarastoni", 1)]
    partial class PreSpawnedGhostsCleanupBaking : SystemBase
    {
        private EntityQuery m_PreviouslyBakedEntities;

        public static ComponentTypeSet PreSpawnedGhostsComponents = new ComponentTypeSet(new ComponentType[]
        {
            typeof(SubSceneGhostComponentHash),
            //Disable the entity so the ghost cannot be retrieved before the prespawn baseline are calculated
            typeof(Disabled),
            typeof(PreSpawnedGhostIndex),
            typeof(PrespawnGhostBaseline),
            typeof(SubSceneWithPrespawnGhosts),
            typeof(PrespawnedGhostBakedBefore)
        });

        protected override void OnCreate()
        {
            base.OnCreate();

            // Query to get all the child entities baked before
            m_PreviouslyBakedEntities = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<PrespawnedGhostBakedBefore>()
                },
                Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab
            });
        }

        protected void RevertPreviousBakings()
        {
            EntityManager.RemoveComponent(m_PreviouslyBakedEntities, PreSpawnedGhostsComponents);
        }

        protected override void OnUpdate()
        {
            // Remove the components added by the baker for the entities not contained in hashToEntity
            RevertPreviousBakings();
        }
    }
}
