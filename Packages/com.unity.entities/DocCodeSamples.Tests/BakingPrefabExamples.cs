using UnityEngine;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Scenes;

// The files in this namespace are used to test the code samples in the documentation.
namespace Doc.CodeSamples.Tests
{

    #region PrefabInSubScene
    public struct EntityPrefabComponent : IComponentData
    {
        public Entity Value;
    }

    public class GetPrefabAuthoring : MonoBehaviour
    {
        public GameObject Prefab;
    }

    public class GetPrefabBaker : Baker<GetPrefabAuthoring>
    {
        public override void Bake(GetPrefabAuthoring authoring)
        {
            // Register the Prefab in the Baker
            var entityPrefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic);
            // Add the Entity reference to a component for instantiation later
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new EntityPrefabComponent() {Value = entityPrefab});
        }
    }
    #endregion

    #region PrefabReferenceInSubScene
    public struct EntityPrefabReferenceComponent : IComponentData
    {
        public EntityPrefabReference Value;
    }

    public class GetPrefabReferenceAuthoring : MonoBehaviour
    {
        public GameObject Prefab;
    }

    public class GetPrefabReferenceBaker : Baker<GetPrefabReferenceAuthoring>
    {
        public override void Bake(GetPrefabReferenceAuthoring authoring)
        {
            // Create an EntityPrefabReference from a GameObject. This will allow the
            // serialization process to serialize the prefab in its own entity scene
            // file instead of duplicating the prefab ECS content everywhere it is used
            var entityPrefab = new EntityPrefabReference(authoring.Prefab);
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new EntityPrefabReferenceComponent() {Value = entityPrefab});
        }
    }
    #endregion

    #region InstantiateEmbeddedPrefabs
    public partial struct InstantiatePrefabSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Get all Entities that have the component with the Entity reference
            foreach (var prefab in
                     SystemAPI.Query<RefRO<EntityPrefabComponent>>())
            {
                // Instantiate the prefab Entity
                var instance = ecb.Instantiate(prefab.ValueRO.Value);
                // Note: the returned instance is only relevant when used in the ECB
                // as the entity is not created in the EntityManager until ECB.Playback
                ecb.AddComponent<ComponentA>(instance);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
    #endregion


    #region InstantiateLoadedPrefabs
    public partial struct InstantiatePrefabReferenceSystem : ISystem
    {
        public void OnStartRunning(ref SystemState state)
        {
            // Add the RequestEntityPrefabLoaded component to the Entities that have an
            // EntityPrefabReference but not yet have the PrefabLoadResult
            // (the PrefabLoadResult is added when the prefab is loaded)
            // Note: it might take a few frames for the prefab to be loaded
            var query = SystemAPI.QueryBuilder()
                .WithAll<EntityPrefabComponent>()
                .WithNone<PrefabLoadResult>().Build();
            state.EntityManager.AddComponent<RequestEntityPrefabLoaded>(query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // For the Entities that have a PrefabLoadResult component (Unity has loaded
            // the prefabs) get the loaded prefab from PrefabLoadResult and instantiate it
            foreach (var (prefab, entity) in
                     SystemAPI.Query<RefRO<PrefabLoadResult>>().WithEntityAccess())
            {
                var instance = ecb.Instantiate(prefab.ValueRO.PrefabRoot);

                // Remove both RequestEntityPrefabLoaded and PrefabLoadResult to prevent
                // the prefab being loaded and instantiated multiple times, respectively
                ecb.RemoveComponent<RequestEntityPrefabLoaded>(entity);
                ecb.RemoveComponent<PrefabLoadResult>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
    #endregion


    public partial struct PrefabsInQueriesSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            #region PrefabsInQueries
            // This query will return all baked entities, including the prefab entities
            var prefabQuery = SystemAPI.QueryBuilder()
                .WithAll<BakedEntity>().WithOptions(EntityQueryOptions.IncludePrefab).Build();
            #endregion

            #region DestroyPrefabs
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (component, entity) in
                     SystemAPI.Query<RefRO<RotationSpeed>>().WithEntityAccess())
            {
                if (component.ValueRO.RadiansPerSecond <= 0)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            #endregion
        }
    }
}
