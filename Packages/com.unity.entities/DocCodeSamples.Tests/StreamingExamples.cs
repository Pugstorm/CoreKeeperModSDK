using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Mathematics;
using Unity.Scenes;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

using LocalTransform = Unity.Transforms.LocalTransform;

namespace Doc.CodeSamples.Tests
{
    public partial class SceneLoading101 : SystemBase
    {
        protected override void OnUpdate()
        {
            #region sceneloading_101

            // Note: calling GetSceneGUID is slow, please keep reading for a proper example.
            var guid = SceneSystem.GetSceneGUID(ref World.Unmanaged.GetExistingSystemState<SceneSystem>(), "Assets/Scenes/SampleScene.unity");
            var sceneEntity = SceneSystem.LoadSceneAsync(World.Unmanaged, guid);

            #endregion
        }
    }

#region sceneloader_component
// Runtime component, SceneSystem uses EntitySceneReference to identify scenes.
public struct SceneLoader : IComponentData
{
    public EntitySceneReference SceneReference;
}

#if UNITY_EDITOR
// Authoring component, a SceneAsset can only be used in the Editor
public class SceneLoaderAuthoring : MonoBehaviour
{
    public UnityEditor.SceneAsset Scene;

    class Baker : Baker<SceneLoaderAuthoring>
    {
        public override void Bake(SceneLoaderAuthoring authoring)
        {
            var reference = new EntitySceneReference(authoring.Scene);
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new SceneLoader
            {
                SceneReference = reference
            });
        }
    }
}
#endif
#endregion

#region sceneloadersystem
    [RequireMatchingQueriesForUpdate]
    public partial class SceneLoaderSystem : SystemBase
    {
        private EntityQuery newRequests;

        protected override void OnCreate()
        {
            newRequests = GetEntityQuery(typeof(SceneLoader));
        }

        protected override void OnUpdate()
        {
            var requests = newRequests.ToComponentDataArray<SceneLoader>(Allocator.Temp);

            // Can't use a foreach with a query as SceneSystem.LoadSceneAsync does structural changes
            for (int i = 0; i < requests.Length; i += 1)
            {
                SceneSystem.LoadSceneAsync(World.Unmanaged, requests[i].SceneReference);
            }

            requests.Dispose();
            EntityManager.DestroyEntity(newRequests);
        }
    }
#endregion

    public partial class SceneLoadingResolveOnly : SystemBase
    {
        protected override void OnUpdate()
        {
            EntitySceneReference sceneReference = default;

            #region sceneloading_resolveonly

            var loadParameters = new SceneSystem.LoadParameters {Flags = SceneLoadFlags.DisableAutoLoad};
            var sceneEntity = SceneSystem.LoadSceneAsync(World.Unmanaged, sceneReference, loadParameters);

            #endregion
        }
    }

    #region section_metadata
    // Component that will store the meta data
    public struct RadiusSectionMetadata : IComponentData
    {
        // Radius to consider to load the section
        public float Radius;

        // Center of the section
        public float3 Position;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    partial struct RadiusSectionMetadataBakingSystem : ISystem
    {
        private EntityQuery sectionEntityQuery;

        public void OnCreate(ref SystemState state)
        {
            // Creating the EntityQuery for SerializeUtility.GetSceneSectionEntity ahead
            sectionEntityQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SectionMetadataSetup>().Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            int section = 3;
            float radius = 10f;
            float3 position = new float3(0f);

            // Accessing the section meta entity
            var sectionEntity = SerializeUtility.GetSceneSectionEntity(section,
                state.EntityManager, ref sectionEntityQuery, true);
            // Adding RadiusSectionMetadata as metadata to the section
            state.EntityManager.AddComponentData(sectionEntity, new RadiusSectionMetadata
            {
                Radius   = radius,
                Position = position
            });
        }
    }
    #endregion

    public partial class SceneLoadingRequestSections : SystemBase
    {
        protected override void OnUpdate()
        {
            Entity sceneEntity = default;

            #region sceneloading_requestsections
            // To keep the sample code short, we assume that the sections have been resolved.
            // And the code that ensures the code runs only once isn't included either.
            var sectionBuffer = EntityManager.GetBuffer<ResolvedSectionEntity>(sceneEntity);
            var sectionEntities = sectionBuffer.ToNativeArray(Allocator.Temp);

            for (int i = 0; i < sectionEntities.Length; i += 1)
            {
                if (i % 2 == 0)
                {
                    // Note that the condition includes section 0,
                    // nothing else will load if section 0 is missing.
                    var sectionEntity = sectionEntities[i].SectionEntity;
                    EntityManager.AddComponent<RequestSceneLoaded>(sectionEntity);
                }
            }

            sectionEntities.Dispose();
            #endregion

            #region sceneloading_unloadscene
            var unloadParameters = SceneSystem.UnloadParameters.DestroyMetaEntities;
            SceneSystem.UnloadScene(World.Unmanaged, sceneEntity, unloadParameters);
            #endregion

            Entity sceneSectionEntity = default;

            #region sceneloading_isstuffloaded
            var sceneLoaded = SceneSystem.IsSceneLoaded(World.Unmanaged, sceneEntity);
            var sectionLoaded = SceneSystem.IsSectionLoaded(World.Unmanaged, sceneSectionEntity);
            #endregion

            #region sceneloading_state
            var sceneState = SceneSystem.GetSceneStreamingState(World.Unmanaged, sceneEntity);
            var sectionState = SceneSystem.GetSectionStreamingState(World.Unmanaged, sceneSectionEntity);
            #endregion
        }
    }

    #region sceneloading_instance_data
    public struct PostLoadOffset : IComponentData
    {
        public float3 Offset;
    }
    #endregion

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        public partial struct LoadWithOffsetSystem : ISystem
        {
            public void OnUpdate(ref SystemState state)
            {
                EntitySceneReference sceneReference = default;
                float3 sceneOffset = default;

#region sceneloading_instancing1_2
                var loadParameters = new SceneSystem.LoadParameters()
                    { Flags = SceneLoadFlags.NewInstance };
                var sceneEntity = SceneSystem.LoadSceneAsync(state.WorldUnmanaged,
                    sceneReference, loadParameters);

                var ecb = new EntityCommandBuffer(Allocator.Persistent,
                    PlaybackPolicy.MultiPlayback);
                var postLoadEntity = ecb.CreateEntity();
                var postLoadOffset = new PostLoadOffset
                {
                    Offset = sceneOffset
                };
                ecb.AddComponent(postLoadEntity, postLoadOffset);

                var postLoadCommandBuffer = new PostLoadCommandBuffer()
                {
                    CommandBuffer = ecb
                };
                state.EntityManager.AddComponentData(sceneEntity, postLoadCommandBuffer);
#endregion
            }
        }
#endif

#region sceneloading_instancing3

    [UpdateInGroup(typeof(ProcessAfterLoadGroup))]
    public partial struct PostprocessSystem : ISystem
    {
        private EntityQuery offsetQuery;

        public void OnCreate(ref SystemState state)
        {
            offsetQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PostLoadOffset>()
                .Build(ref state);
            state.RequireForUpdate(offsetQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            // Query the instance information from the entity created in the EntityCommandBuffer.
            var offsets = offsetQuery.ToComponentDataArray<PostLoadOffset>(Allocator.Temp);
            foreach (var offset in offsets)
            {
                // Use that information to apply the transforms to the entities in the instance.
                foreach (var transform in SystemAPI.Query<RefRW<LocalTransform>>())
                {
                    transform.ValueRW.Position += offset.Offset;
                }
            }
            state.EntityManager.DestroyEntity(offsetQuery);
        }
    }
#endregion

}
