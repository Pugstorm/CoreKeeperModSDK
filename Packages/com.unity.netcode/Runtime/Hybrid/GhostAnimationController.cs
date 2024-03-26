using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

using Unity.Entities;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Transforms;

namespace Unity.NetCode.Hybrid
{
    /// <summary>
    /// A component used by the GhostAnimationController to figure out what needs a
    /// managed update call and what can use a system based fast path.
    /// </summary>
    public struct EnableAnimationControllerPredictionUpdate : IComponentData
    {}

    /// <summary>
    /// A ghost animation controller is a special animation graph which supports
    /// ghosting through netcode for entities. It needs to be added to a GameObject
    /// which is referenced by an entity through a GhostPresentationGameObjectPrefabReference.
    /// The controller has a single graph asset, but that asset can be recursive
    /// and contain a full graph.
    /// </summary>
    [RequireComponent(typeof(Animator), typeof(GhostPresentationGameObjectEntityOwner))]
    [DisallowMultipleComponent]
    [HelpURL(HelpURLs.GhostAnimationController)]
    public class GhostAnimationController : MonoBehaviour, IRegisterPlayableData
    {
        interface IAnimationDataReference : IDisposable
        {
            void CopyFromEntity(EntityManager entityManager, Entity entity);
            void CopyToEntity(EntityManager entityManager, Entity entity);
        }
        class AnimationDataReference<T> : IAnimationDataReference where T: unmanaged, IComponentData
        {
            public NativeReference<T> Value;
            public void CopyFromEntity(EntityManager entityManager, Entity entity)
            {
                Value.Value = entityManager.GetComponentData<T>(entity);
            }
            public void CopyToEntity(EntityManager entityManager, Entity entity)
            {
                entityManager.SetComponentData(entity, Value.Value);
            }
            public void Dispose()
            {
                Value.Dispose();
            }
        }

        /// <summary>
        /// The graph asset used by this controller.
        /// </summary>
        public GhostAnimationGraphAsset AnimationGraphAsset;
        /// <summary>
        /// Setting this to true will cause the animation graph to be evaluated as part of
        /// the prediction update. Doing that will give you immediate updated to the skeleton,
        /// if it is set to false the pose will only be update once per frame after all systems
        /// have run, so it has a one frame latency. Setting it to false will also prevet
        /// root motion from working.
        /// </summary>
        public bool EvaluateGraphInPrediction;
        /// <summary>
        /// Setting this to true will prevent the animation system from firing events even
        /// if they are specified in the animation nodes. This is mostly useful if you are
        /// re-using an asset with events but are not handling the events in the entities
        /// version.
        /// </summary>
        public bool IgnoreEvents;
        private bool m_ApplyRootMotion;
        /// <summary>
        /// Returns true if root motion is being used by this controller. It is only
        /// true if the animator supports it, the graph is evaluated in prediction
        /// and the ghost is predicted (when using owner prediction the local players
        /// character is predicted).
        /// Can be accessed from graph assets to modify behaviour when root motion is enabled.
        /// </summary>
        public bool ApplyRootMotion => m_ApplyRootMotion;

        private GhostPresentationGameObjectEntityOwner m_EntityOwner;
        private PlayableGraph m_PlayableGraph;
        internal List<GhostPlayableBehaviour> m_PlayableBehaviours;

        Dictionary<Type, IAnimationDataReference> m_References = new Dictionary<Type, IAnimationDataReference>();
        #if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal bool m_IsPredictionUpdate;
        #endif

        private Transform m_Transform;

        /// <summary>
        /// Implementation of IRegisterPlayableData, should not be called directly.
        /// </summary>
        /// <typeparam name="T">Unmanaged type of <see cref="IComponentData"/>.</typeparam>
        public void RegisterPlayableData<T>() where T: unmanaged, IComponentData
        {
            if (!m_EntityOwner.World.EntityManager.HasComponent<T>(m_EntityOwner.Entity))
                throw new InvalidOperationException("Playable data registration failed");
            if (!m_References.ContainsKey(typeof(T)))
            {
                // Allocate memory for a copy of the data
                var reference = new AnimationDataReference<T>();
                reference.Value = new NativeReference<T>(Allocator.Persistent);
                m_References[typeof(T)] = reference;
            }
        }
        /// <summary>
        /// Get a copy of playable data registered by the graph asset.
        /// This can be called at any time.
        /// </summary>
        /// <typeparam name="T">Unmanaged type of <see cref="IComponentData"/>.</typeparam>
        /// <returns>Copy of playable data of type <typeparamref name="T"/>.</returns>
        public unsafe T GetPlayableData<T>() where T: unmanaged, IComponentData
        {
            if (!m_References.ContainsKey(typeof(T)))
                throw new InvalidOperationException($"Trying to get playable data of type {typeof(T)}, but it has not been registered");
            AnimationDataReference<T> reference = m_References[typeof(T)] as AnimationDataReference<T>;
            return reference.Value.Value;
        }
        /// <summary>
        /// Get a reference to playable data registered by the graph asset.
        /// This can only be called from PreparePredictedData.
        /// </summary>
        /// <typeparam name="T">Unmanaged type of <see cref="IComponentData"/>.</typeparam>
        /// <returns>Reference to playable data of type <typeparamref name="T"/>.</returns>
        public unsafe ref T GetPlayableDataRef<T>() where T: unmanaged, IComponentData
        {
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!m_IsPredictionUpdate)
                throw new InvalidOperationException("GetPlayableDataRef can only be called from PreparePredictedData, use GetPlayableData without ref to read the data outside the prediction update");
            #endif
            AnimationDataReference<T> reference = m_References[typeof(T)] as AnimationDataReference<T>;
            // Lookup the pointer, convert to ref and return
            return ref UnsafeUtility.AsRef<T>(reference.Value.GetUnsafePtr());
        }
        /// <summary>
        /// Get a copy of data for a component on the entity associated with the controller.
        /// This can only be called from PreparePredictedData.
        /// </summary>
        /// <typeparam name="T">Unmanaged type of <see cref="IComponentData"/>.</typeparam>
        /// <returns>Copy of component data of type <typeparamref name="T"/>.</returns>
        public T GetEntityComponentData<T>() where T: unmanaged, IComponentData
        {
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!m_IsPredictionUpdate)
                throw new InvalidOperationException("Reading entity data is only allowed from PreparePredictedData, use RegisterPlayableData/GetPlayableData to access data outside the prediction loop");
            #endif
            return m_EntityOwner.World.EntityManager.GetComponentData<T>(m_EntityOwner.Entity);
        }
        /// <summary>
        /// Modify the data for a component on the entity associated with the controller.
        /// This can only be called from PreparePredictedData.
        /// </summary>
        /// <param name="data">Data to assign to the entity.</param>
        /// <typeparam name="T">Unmanaged type of <see cref="IComponentData"/>.</typeparam>
        public void SetEntityComponentData<T>(T data) where T: unmanaged, IComponentData
        {
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!m_IsPredictionUpdate)
                throw new InvalidOperationException("Writing entity data is only allowed from PreparePredictedData");
            #endif
            m_EntityOwner.World.EntityManager.SetComponentData<T>(m_EntityOwner.Entity, data);
        }
        /// <summary>
        /// Get a DynamicBuffer for a component on the entity associated with the controller.
        /// This can only be called from PreparePredictedData.
        /// </summary>
        /// <typeparam name="T">Unmanaged type of <see cref="IBufferElementData"/>.</typeparam>
        /// <returns><see cref="DynamicBuffer{T}"/> of components of type <typeparamref name="T"/> on controller.</returns>
        public DynamicBuffer<T> GetEntityBuffer<T>() where T: unmanaged, IBufferElementData
        {
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!m_IsPredictionUpdate)
                throw new InvalidOperationException("Reading entity data is only allowed from PreparePredictedData, use RegisterPlayableData/GetPlayableData to access data outside the prediction loop");
            #endif
            return m_EntityOwner.World.EntityManager.GetBuffer<T>(m_EntityOwner.Entity);
        }
        internal void CopyFromEntities()
        {
            foreach (var entry in m_References.Values)
                entry.CopyFromEntity(m_EntityOwner.World.EntityManager, m_EntityOwner.Entity);
        }
        internal void CopyToEntities()
        {
            foreach (var entry in m_References.Values)
                entry.CopyToEntity(m_EntityOwner.World.EntityManager, m_EntityOwner.Entity);
        }

        internal void EvaluateGraph(float deltaTime)
        {
            if (m_PlayableBehaviours == null)
                return;
            if (m_ApplyRootMotion)
            {
                m_Transform.localPosition = m_EntityOwner.World.EntityManager.GetComponentData<LocalTransform>(m_EntityOwner.Entity).Position;
                m_Transform.localRotation = m_EntityOwner.World.EntityManager.GetComponentData<LocalTransform>(m_EntityOwner.Entity).Rotation;
            }
            m_PlayableGraph.Evaluate(deltaTime);
            if (m_ApplyRootMotion)
            {
                m_EntityOwner.World.EntityManager.SetComponentData(m_EntityOwner.Entity, LocalTransform.FromPositionRotation(m_Transform.localPosition, m_Transform.localRotation));
            }
        }

        void Start()
        {
            m_Transform = gameObject.transform;
            var animator = GetComponent<Animator>();
            m_EntityOwner = GetComponent<GhostPresentationGameObjectEntityOwner>();
            var isPredicted = m_EntityOwner.World.EntityManager.HasComponent<PredictedGhost>(m_EntityOwner.Entity);
            // Create the playable graph from the asset
            m_PlayableGraph = PlayableGraph.Create(gameObject.name);

            if (IgnoreEvents)
                animator.fireEvents = false;

            //update the graph manually
            if (EvaluateGraphInPrediction && isPredicted)
                m_PlayableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            else
            {
                // Disable root motion for interpolated ghosts, or when updating in prediction is not enabled
                animator.applyRootMotion = false;
                m_PlayableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            }
            m_ApplyRootMotion = animator.applyRootMotion;

            m_PlayableBehaviours = new List<GhostPlayableBehaviour>();
            AnimationGraphAsset.RegisterPlayableData(this);
            var playable = AnimationGraphAsset.CreatePlayable(this, m_PlayableGraph, m_PlayableBehaviours);

            var playableOutput = AnimationPlayableOutput.Create(m_PlayableGraph, "Animator", animator);
            playableOutput.SetSourcePlayable(playable, 0);

            m_PlayableGraph.Play();

            if (m_PlayableBehaviours.Count > 0 || EvaluateGraphInPrediction)
                m_EntityOwner.World.EntityManager.AddComponentData(m_EntityOwner.Entity, default(EnableAnimationControllerPredictionUpdate));
        }

        void OnDestroy()
        {
            // Destroy the playable graph
            m_PlayableGraph.Destroy();
            foreach (var entry in m_References.Values)
                entry.Dispose();
            m_References.Clear();
        }
    }

    /// <summary>
    /// A system which calls PreparePredictedData for all registered ghost animation controllers
    /// and also trigger graph evaluation if enabled.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    public partial class GhostAnimationControllerPredictionSystem : SystemBase
    {
        GhostPresentationGameObjectSystem m_GhostPresentationGameObjectSystem;
        protected override void OnCreate()
        {
            m_GhostPresentationGameObjectSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
            RequireForUpdate(GetEntityQuery(ComponentType.ReadOnly<GhostPresentationGameObjectPrefabReference>(), ComponentType.ReadOnly<EnableAnimationControllerPredictionUpdate>()));
        }
        protected override void OnUpdate()
        {
            var predictionTick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;
            var prevTick = predictionTick;
            prevTick.Decrement();
            var deltaTime = SystemAPI.Time.DeltaTime;
            Entities
                .WithoutBurst()
                .WithAll<GhostPresentationGameObjectPrefabReference>()
                .WithAll<EnableAnimationControllerPredictionUpdate>()
                .WithAll<Simulate>()
                .ForEach((Entity entity, in PredictedGhost predict) => {
                    var isRollback = !predict.ShouldPredict(prevTick);
                    var go = m_GhostPresentationGameObjectSystem.GetGameObjectForEntity(EntityManager, entity);
                    var ctrl = go?.GetComponent<GhostAnimationController>();
                    if (ctrl == null)
                        return;
                    ctrl.CopyFromEntities();
                    if (ctrl.m_PlayableBehaviours.Count > 0)
                    {
                        #if ENABLE_UNITY_COLLECTIONS_CHECKS
                        ctrl.m_IsPredictionUpdate = true;
                        #endif
                        foreach (var behaviour in ctrl.m_PlayableBehaviours)
                            behaviour.PreparePredictedData(predictionTick, deltaTime, isRollback);
                        #if ENABLE_UNITY_COLLECTIONS_CHECKS
                        ctrl.m_IsPredictionUpdate = false;
                        #endif
                        ctrl.CopyToEntities();
                    }
                    if (ctrl.EvaluateGraphInPrediction)
                    {
                        ctrl.EvaluateGraph(deltaTime);
                    }
                }).Run();
        }
    }
    /// <summary>
    /// A system which makes sure registered playable data is updated before
    /// PrepareFrame runs for interpolated ghosts, and predicted ghosts not using
    /// PreparePredictedData or graph updates in prediction.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class GhostAnimationControllerInterpolationSystem : SystemBase
    {
        GhostPresentationGameObjectSystem m_GhostPresentationGameObjectSystem;
        protected override void OnCreate()
        {
            m_GhostPresentationGameObjectSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
            RequireForUpdate<GhostPresentationGameObjectPrefabReference>();
        }
        protected override void OnUpdate()
        {
            Entities
                .WithoutBurst()
                .WithAll<GhostPresentationGameObjectPrefabReference>()
                .WithNone<PredictedGhost>()
                .ForEach((Entity entity) => {
                    var go = m_GhostPresentationGameObjectSystem.GetGameObjectForEntity(EntityManager, entity);
                    var ctrl = go?.GetComponent<GhostAnimationController>();
                    if (ctrl != null)
                        ctrl.CopyFromEntities();
                }).Run();
            Entities
                .WithoutBurst()
                .WithAll<GhostPresentationGameObjectPrefabReference>()
                .WithAll<PredictedGhost>()
                .WithNone<EnableAnimationControllerPredictionUpdate>()
                .ForEach((Entity entity) => {
                    var go = m_GhostPresentationGameObjectSystem.GetGameObjectForEntity(EntityManager, entity);
                    var ctrl = go?.GetComponent<GhostAnimationController>();
                    if (ctrl != null && ctrl.m_PlayableBehaviours != null && ctrl.m_PlayableBehaviours.Count == 0)
                        ctrl.CopyFromEntities();
                }).Run();
        }
    }
    /// <summary>
    /// A system which makes sure registered playable data is updated before
    /// PrepareFrame runs for predicted ghosts not using PreparePredictedData
    /// or graph updates in prediction on the server.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast=true)]
    [UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial class GhostAnimationControllerServerSystem : SystemBase
    {
        GhostPresentationGameObjectSystem m_GhostPresentationGameObjectSystem;
        protected override void OnCreate()
        {
            m_GhostPresentationGameObjectSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
            RequireForUpdate<GhostPresentationGameObjectPrefabReference>();
        }
        protected override void OnUpdate()
        {
            Entities
                .WithoutBurst()
                .WithAll<GhostPresentationGameObjectPrefabReference>()
                .WithAll<PredictedGhost>()
                .WithNone<EnableAnimationControllerPredictionUpdate>()
                .ForEach((Entity entity) => {
                    var go = m_GhostPresentationGameObjectSystem.GetGameObjectForEntity(EntityManager, entity);
                    var ctrl = go?.GetComponent<GhostAnimationController>();
                    if (ctrl != null && ctrl.m_PlayableBehaviours != null && ctrl.m_PlayableBehaviours.Count == 0)
                        ctrl.CopyFromEntities();
                }).Run();
        }
    }
}
