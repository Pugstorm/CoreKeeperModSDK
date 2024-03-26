using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;

namespace Unity.NetCode.Editor
{
    /// <summary>
    /// Process in the editor any sub-scene open for edit that contains pre-spawned ghosts.
    /// This is a work-around for a limitation in the conversion workdflow that prevent custom component added to
    /// sceen section entity when a sub-scene is open for edit.
    /// To overcome that, the SubSceneWithPrespawnGhosts is added at runtime here and a LiveLinkPrespawnSectionReference
    /// is also added ot the scene section enity to provide some misisng information about the section is referring to.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct PrespawnedGhostPreprocessScene : ISystem
    {
        struct PrespawnSceneExtracted : IComponentData
        {
        }
        //SceneSystem.SectionLoadedFromEntity m_SectionLoadedFromEntity;
        private EntityQuery prespawnToPreprocess;
        private EntityQuery sectionsToProcess;
        private SharedComponentTypeHandle<SubSceneGhostComponentHash> prespawnHashTypeHandle;
        private SharedComponentTypeHandle<SceneSection> sceneSectionTypeHandle;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Prerequisite: must exist some prespawn, otherwise no need to run
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PreSpawnedGhostIndex, SceneTag>()
                .WithAllRW<SubSceneGhostComponentHash>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities);
            prespawnToPreprocess = state.GetEntityQuery(builder);

            builder.Reset();
            builder.WithAll<DisableSceneResolveAndLoad, SceneEntityReference>()
                .WithNone<SceneSectionData, PrespawnSceneExtracted, SubSceneWithPrespawnGhosts>();
            sectionsToProcess = state.GetEntityQuery(builder);

            prespawnHashTypeHandle = state.GetSharedComponentTypeHandle<SubSceneGhostComponentHash>();
            sceneSectionTypeHandle = state.GetSharedComponentTypeHandle<SceneSection>();
            state.RequireForUpdate(prespawnToPreprocess);
            state.RequireForUpdate(sectionsToProcess);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            //this is only valid in the editor
            prespawnHashTypeHandle.Update(ref state);
            sceneSectionTypeHandle.Update(ref state);
            prespawnToPreprocess.ResetFilter();
            var sceneEntities = sectionsToProcess.ToEntityArray(Allocator.Temp);
            foreach (var sectionEntity in sceneEntities)
            {
                if(!state.EntityManager.HasComponent<IsSectionLoaded>(sectionEntity))
                    continue;

                prespawnToPreprocess.SetSharedComponentFilter(new SceneTag{SceneEntity = sectionEntity});
                // Mark the scene as processed. All scene section must be marked
                state.EntityManager.AddComponent<PrespawnSceneExtracted>(sectionEntity);
                // Early exit if no prespawn are present
                var count = prespawnToPreprocess.CalculateEntityCount();
                if (count == 0)
                    continue;

                using var chunks = prespawnToPreprocess.ToArchetypeChunkArray(Allocator.Temp);
                var prespawnGhostHash = chunks[0].GetSharedComponent(prespawnHashTypeHandle);
                var sceneSection = chunks[0].GetSharedComponent(sceneSectionTypeHandle);
                state.EntityManager.AddComponentData(sectionEntity, new SubSceneWithPrespawnGhosts
                {
                    SubSceneHash = prespawnGhostHash.Value,
                    BaselinesHash = 0,
                    PrespawnCount = count
                });
                //Add this component to allow retrieve the section index and scene guid. This information are necessary
                //to correctly add the SceneSection component to the pre-spawned ghosts when they are re-spawned
                //FIXME: investigate if using the SceneTag may be sufficient to guaratee that re-spawned prespawned ghosts
                //are deleted when scenes are unloaded. We can the remove this component and further simplify other things
                state.EntityManager.AddComponentData(sectionEntity, new LiveLinkPrespawnSectionReference
                {
                    SceneGUID = sceneSection.SceneGUID,
                    Section = sceneSection.Section
                });
            }
        }
    }
}
