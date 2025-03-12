using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Extensions;
using Unity.Physics.GraphicsIntegration;
using Unity.Transforms;
using UnityEngine;

namespace Unity.Physics.Authoring
{
    /// <summary>
    /// Component that specifies if a rigid body is a kinematic body and what its mass is.
    /// </summary>
    [TemporaryBakingType]
    public struct RigidbodyBakingData : IComponentData
    {
        /// <summary> Sets rigid body as kinematic body. </summary>
        public bool isKinematic;

        /// <summary> Mass of the rigid body. </summary>
        public float mass;

        /// <summary> Center of mass of the rigid body is automatically calculated if this is true. </summary>
        public bool automaticCenterOfMass;

        /// <summary> Center of mass of the rigid body. Used if automaticCenterOfMass is false. </summary>
        public float3 centerOfMass;

        /// <summary> Inertia tensor of the rigid body is automatically calculated if this is true. </summary>
        public bool automaticInertiaTensor;

        /// <summary> Inertia tensor of the rigid body. Used if automaticInertiaTensor is false. </summary>
        public float3 inertiaTensor;

        /// <summary> Rotation of the inertia tensor. Used if automaticInertiaTensor is false. </summary>
        public quaternion inertiaTensorRotation;
    }

    /// <summary>
    /// A baker for a Rigidbody component
    /// </summary>
    class RigidbodyBaker : BasePhysicsBaker<Rigidbody>
    {
        static List<UnityEngine.Collider> colliderComponents = new List<UnityEngine.Collider>();

        public override void Bake(Rigidbody authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            var bakingData = new RigidbodyBakingData
            {
                isKinematic = authoring.isKinematic,
                mass = authoring.mass,
                automaticCenterOfMass = authoring.automaticCenterOfMass,
                centerOfMass = authoring.centerOfMass,
                automaticInertiaTensor = authoring.automaticInertiaTensor,
                inertiaTensor = authoring.inertiaTensor,
                inertiaTensorRotation = authoring.inertiaTensorRotation
            };
            AddComponent(entity, bakingData);

            AddSharedComponent(entity, new PhysicsWorldIndex());

            var bodyTransform = GetComponent<Transform>();

            var motionType = authoring.isKinematic ? BodyMotionType.Kinematic : BodyMotionType.Dynamic;
            var hasInterpolation = authoring.interpolation != RigidbodyInterpolation.None;
            PostProcessTransform(bodyTransform, motionType);

            // Check that there is at least one collider in the hierarchy to add these three
            GetComponentsInChildren(colliderComponents);
            if (colliderComponents.Count > 0)
            {
                AddComponent(entity, new PhysicsCompoundData()
                {
                    AssociateBlobToBody = false,
                    ConvertedBodyInstanceID = authoring.GetInstanceID(),
                    Hash = default,
                });
                AddComponent<PhysicsRootBaked>(entity);
                AddComponent<PhysicsCollider>(entity);
            }

            // Ignore the rest if the object is static
            if (IsStatic())
                return;

            if (hasInterpolation)
            {
                AddComponent(entity, new PhysicsGraphicalSmoothing());

                if (authoring.interpolation == RigidbodyInterpolation.Interpolate)
                {
                    AddComponent(entity, new PhysicsGraphicalInterpolationBuffer
                    {
                        PreviousTransform = Math.DecomposeRigidBodyTransform(bodyTransform.localToWorldMatrix)
                    });
                }
            }

            // Add default PhysicsMass component. The actual mass properties values will be set by the RigidbodyBakingSystem.
            var massProperties = MassProperties.UnitSphere;
            AddComponent(entity, !authoring.isKinematic ?
                PhysicsMass.CreateDynamic(massProperties, authoring.mass) :
                PhysicsMass.CreateKinematic(massProperties));

            AddComponent(entity, new PhysicsVelocity());

            if (!authoring.isKinematic)
            {
                AddComponent(entity, new PhysicsDamping
                {
#if UNITY_2023_3_OR_NEWER
                    Linear = authoring.linearDamping,
                    Angular = authoring.angularDamping
#else
                    Linear = authoring.drag,
                    Angular = authoring.angularDrag
#endif
                });
                if (!authoring.useGravity)
                    AddComponent(entity, new PhysicsGravityFactor { Value = 0f });
            }
            else
                AddComponent(entity, new PhysicsGravityFactor { Value = 0 });
        }
    }

    /// <summary>
    /// Represents a rigidbody baking system.
    /// </summary>
    [RequireMatchingQueriesForUpdate]
    [UpdateAfter(typeof(EndColliderBakingSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct RigidbodyBakingSystem : ISystem
    {
        static SimulationMode k_InvalidSimulationMode = (SimulationMode) ~0;
        SimulationMode m_SavedSmulationMode;
        bool m_ProcessSimulationModeChange;

        public void OnCreate(ref SystemState state)
        {
            m_SavedSmulationMode = k_InvalidSimulationMode;

            m_ProcessSimulationModeChange = Application.isPlaying;
        }

        public void OnDestroy(ref SystemState state)
        {
            // Unless no legacy physics step data is available, restore previously stored legacy physics simulation mode
            // when leaving play-mode.
            if (m_SavedSmulationMode != k_InvalidSimulationMode)
            {
                UnityEngine.Physics.simulationMode = m_SavedSmulationMode;
                m_SavedSmulationMode = k_InvalidSimulationMode;
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            if (m_ProcessSimulationModeChange)
            {
                // When entering playmode, cache and override legacy physics simulation mode, disabling legacy physics and this way
                // preventing it from running while playing with open sub-scenes. Otherwise, the legacy physics simulation
                // will overwrite the last known edit mode state of game objects in the sub-scene with its simulation results.
                m_SavedSmulationMode = UnityEngine.Physics.simulationMode;
                UnityEngine.Physics.simulationMode = SimulationMode.Script;

                m_ProcessSimulationModeChange = false;
            }

            // Set world index for bodies with world index baking data
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            foreach (var(rigidBodyData, worldIndexData, entity) in
                     SystemAPI.Query<RefRO<RigidbodyBakingData>, RefRO<PhysicsWorldIndexBakingData>>()
                         .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities).WithEntityAccess())
            {
                ecb.SetSharedComponent(entity, new PhysicsWorldIndex(worldIndexData.ValueRO.WorldIndex));
            }

            var entityManager = state.EntityManager;

            // Set mass properties for rigid bodies without collider
            foreach (var(physicsMass, bodyData, entity) in
                     SystemAPI.Query<RefRW<PhysicsMass>, RefRO<RigidbodyBakingData>>()
                         .WithEntityAccess()
                         .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities))
            {
                physicsMass.ValueRW = CreatePhysicsMass(entityManager, entity, bodyData.ValueRO, MassProperties.UnitSphere);
            }

            // Set mass properties for rigid bodies with collider
            foreach (var(physicsMass, bodyData, collider, entity) in
                     SystemAPI.Query<RefRW<PhysicsMass>, RefRO<RigidbodyBakingData>, RefRO<PhysicsCollider>>()
                         .WithEntityAccess()
                         .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities))
            {
                physicsMass.ValueRW = CreatePhysicsMass(entityManager, entity, bodyData.ValueRO, collider.ValueRO.MassProperties);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private PhysicsMass CreatePhysicsMass(EntityManager entityManager, in Entity entity,
            in RigidbodyBakingData inBodyData, in MassProperties inMassProperties)
        {
            var massProperties = inMassProperties;
            var scale = 1f;

            // Scale the provided mass properties by the LocalTransform.Scale value to create the correct
            // initial mass distribution for the rigid body.
            if (entityManager.HasComponent<LocalTransform>(entity))
            {
                var localTransform = entityManager.GetComponentData<LocalTransform>(entity);
                scale = localTransform.Scale;

                massProperties.Scale(scale);
            }

            // Override the mass properties with user-provided values if specified
            if (!inBodyData.automaticCenterOfMass)
            {
                massProperties.MassDistribution.Transform.pos = inBodyData.centerOfMass;
            }

            if (!inBodyData.automaticInertiaTensor)
            {
                massProperties.MassDistribution.InertiaTensor = inBodyData.inertiaTensor;
                massProperties.MassDistribution.Transform.rot = inBodyData.inertiaTensorRotation;
            }

            // Create the physics mass properties. Among others, this scales the unit mass inertia tensor
            // by the scalar mass of the rigid body.
            var physicsMass = !inBodyData.isKinematic ?
                PhysicsMass.CreateDynamic(massProperties, inBodyData.mass) :
                PhysicsMass.CreateKinematic(massProperties);

            // Now, apply inverse scale to the final, baked physics mass properties in order to prevent invalid simulated mass properties
            // caused by runtime scaling of the mass properties later on while building the physics world.
            physicsMass = physicsMass.ApplyScale(math.rcp(scale));

            return physicsMass;
        }
    }
}
