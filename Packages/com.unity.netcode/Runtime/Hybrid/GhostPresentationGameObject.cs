using Unity.Entities;
using UnityEngine;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine.Jobs;

namespace Unity.NetCode.Hybrid
{
    /// <summary>
    /// The GameObject prefabs which should be used as a visual representation of an entity.
    /// </summary>
    public class GhostPresentationGameObjectPrefab : IComponentData
    {
        /// <summary>
        /// The GameObject prefab which should be used as a visual representation of an entity on the server.
        /// Since this is the server instance it should usually not be visible, but it is needed to for example
        /// run animations on the server.
        /// </summary>
        public GameObject Server;
        /// <summary>
        /// The GameObject prefab which should be used as a visual representation of an entity on the client.
        /// It is not possible to have separate GameObjects for interpolated and predicted ghosts, doing
        /// that would break prediction switching.
        /// </summary>
        public GameObject Client;
    }
    /// <summary>
    /// A reference to an entity containing the GhostPresentationGameObjectPrefab. The GameObject prefabs
    /// are not stored directly on the ghosts to avoid all ghosts having a managed component, instead
    /// a separate entity is created for storing the managed component and this component has a reference
    /// to that entity.
    /// </summary>
    public struct GhostPresentationGameObjectPrefabReference : IComponentData
    {
        /// <summary>
        /// The entity containing the GameObject prefabs to instantiate.
        /// </summary>
        public Entity Prefab;
    }
    /// <summary>
    /// Internal state tracking which GameObject prefabs have been initialized.
    /// </summary>
    internal struct GhostPresentationGameObjectState : ICleanupComponentData
    {
        /// <summary>
        /// Index used by the <see cref="GhostPresentationGameObjectSystem"/> to retrieve the instantiated
        /// GameObject for this entity.
        /// </summary>
        public int GameObjectIndex;
    }

    /// <summary>
    /// This system will spawn presentation game object for ghosts which requested it.
    /// The system runs right after the client spawning code to make sure the game object is created right away.
    /// On the server it is important to either deal with not having a presentation game object (by filtering on the cleanup component)
    /// or do all spawning in the BeginSimulationCommandBufferSystem to make sure the game objects are created at the same time
    /// </summary>
    // This is right after GhostSpawnSystemGroup on a client and right after BeginSimulationEntityCommandBufferSystem on server
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(NetworkReceiveSystemGroup), OrderFirst = true)]
    public partial class GhostPresentationGameObjectSystem : SystemBase
    {
        private List<GameObject> m_GameObjects;
        internal TransformAccessArray m_Transforms;
        internal NativeList<Entity> m_Entities;

        /// <summary>
        /// Lookup the presentation GameObject for a specific entity. The entity
        /// does not have a direct reference to the GameObject, it needs to go through
        /// this method to find it.
        /// </summary>
        /// <param name="entityManager">For looking up the entity <paramref name="ent"/>.</param>
        /// <param name="ent">Entity to find presentation <see cref="GameObject"/> for.</param>
        /// <returns><see cref="GameObject"/> for entity.</returns>
        public GameObject GetGameObjectForEntity(EntityManager entityManager, Entity ent)
        {
            if (!entityManager.HasComponent<GhostPresentationGameObjectState>(ent))
                return null;
            var idx = entityManager.GetComponentData<GhostPresentationGameObjectState>(ent).GameObjectIndex;
            if (idx < 0)
                return null;
            return m_GameObjects[idx];
        }
        protected override void OnCreate()
        {
            m_GameObjects = new List<GameObject>();
            // The values for capacity and desired job count have not been heavily optimized
            m_Transforms = new TransformAccessArray(16, 16);
            m_Entities = new NativeList<Entity>(16, Allocator.Persistent);
        }
        protected override void OnDestroy()
        {
            foreach (var go in m_GameObjects)
            {
                Object.Destroy(go);
            }
            m_GameObjects.Clear();
            m_Transforms.Dispose();
            m_Entities.Dispose();
        }
        protected override void OnUpdate()
        {
            Entities
                .WithStructuralChanges()
                .WithoutBurst()
                .WithNone<GhostPresentationGameObjectState>()
                .ForEach((Entity entity, in GhostPresentationGameObjectPrefabReference presentation) =>
            {
                var goPrefabEntity = EntityManager.GetComponentData<GhostPresentationGameObjectPrefab>(presentation.Prefab);
                var goPrefab = World.IsServer() ? goPrefabEntity.Server : goPrefabEntity.Client;
                int idx = -1;
                if (goPrefab != null)
                {
                    var go = GameObject.Instantiate(goPrefab);
                    var owner = go.GetComponent<GhostPresentationGameObjectEntityOwner>();
                    if (owner != null)
                    {
                        owner.Initialize(entity, World);
                    }
                    idx = m_GameObjects.Count;
                    m_GameObjects.Add(go);
                    m_Entities.Add(entity);
                    m_Transforms.Add(go.transform);
                }
                EntityManager.AddComponentData(entity, new GhostPresentationGameObjectState{GameObjectIndex = idx});
            }).Run();

            var ghostPresentationGameObjectStateFromEntity = GetComponentLookup<GhostPresentationGameObjectState>();
            Entities
                .WithStructuralChanges()
                .WithNone<GhostPresentationGameObjectPrefabReference>()
                .WithAll<GhostPresentationGameObjectState>()
                .ForEach((Entity entity) =>
            {
                var state = ghostPresentationGameObjectStateFromEntity[entity];
                int idx = state.GameObjectIndex;
                if (idx >= 0)
                {
                    m_Transforms.RemoveAtSwapBack(idx);
                    m_Entities.RemoveAtSwapBack(idx);
                    var last = m_GameObjects.Count - 1;
                    Object.Destroy(m_GameObjects[idx]);
                    m_GameObjects[idx] = m_GameObjects[last];
                    ghostPresentationGameObjectStateFromEntity[m_Entities[idx]] = new GhostPresentationGameObjectState{GameObjectIndex = idx};
                    m_GameObjects.RemoveAt(last);
                }
                EntityManager.RemoveComponent<GhostPresentationGameObjectState>(entity);
            }).Run();
        }
    }

    /// <summary>
    /// This system will update the presentation GameObjects transform based on the current transform
    /// of the entity owning it.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(TransformSystemGroup))]
    [UpdateAfter(typeof(LocalToWorldSystem))]
    public partial class GhostPresentationGameObjectTransformSystem : SystemBase
    {
        private GhostPresentationGameObjectSystem m_GhostPresentationGameObjectSystem;
        protected override void OnCreate()
        {
            m_GhostPresentationGameObjectSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
            RequireForUpdate(GetEntityQuery(ComponentType.ReadOnly<GhostPresentationGameObjectPrefabReference>()));
        }
        [BurstCompile]
        struct TransformUpdateJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeList<Entity> Entities;
            //Why we need to use LocalToWorld here and it work. Both Physics and Netcode for Entities
            //can alter the perceived position of the Entity on screen by modifying directly the
            //LTW. In particular: Physics interpolation/extrapolation, Prediction switching.
            //Because of that, the entity and its rendering can be "out of sync" (1d for simplicity):
            //
            //  (interpolated/prediction switching)
            //   |       (S)
            //   |     (D)
            //   | ------------------
            //
            //  (exrapolated)
            //   |     (S)
            //   |       (D)
            //   | ------------------
            //
            //  [Simulated Entity (S)]
            //  [Displayed Entity (D)]
            //
            // The GameObject is the representation of the entity on the screen.
            // We need to decide where we should render it. We can either use:
            // - The local position of the entity (simulated)
            // - The "perceived" one (LTW)
            // The correct answer is actually even simpler: we need to have the rendered position in sync.
            // So, the GameObject position MUST be taken from the LTW. That is a world position.
            // However, because usually the GameObject is a root one (no parent) we can set the LocalPosition
            // instead of Position.
            //
            [ReadOnly] public ComponentLookup<LocalToWorld> TransformFromEntity;
            public void Execute(int index, TransformAccess transform)
            {
                var ent = Entities[index];
                transform.localPosition = TransformFromEntity[ent].Position;
                transform.localRotation = TransformFromEntity[ent].Rotation;
            }
        }
        protected override void OnUpdate()
        {
            var transformJob = new TransformUpdateJob
            {
                Entities = m_GhostPresentationGameObjectSystem.m_Entities,
                TransformFromEntity = GetComponentLookup<LocalToWorld>(true),
            };
            Dependency = transformJob.Schedule(m_GhostPresentationGameObjectSystem.m_Transforms, Dependency);
        }
    }
}
