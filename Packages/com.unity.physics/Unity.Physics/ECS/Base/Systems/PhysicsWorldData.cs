using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace Unity.Physics.Systems
{
    /// <summary>
    /// Structure containing PhysicsWorld and other data and queries that are necessary for
    /// simulating a physics world. Note: it is important to create <see cref="PhysicsWorldData"/>
    /// and use it (to schedule physics world build) in the same system. Creating it in one system,
    /// and calling the Schedule() methods in another can cause race conditions.
    /// </summary>
    public struct PhysicsWorldData : IDisposable
    {
        /// <summary>   The physics world. </summary>
        public PhysicsWorld PhysicsWorld;

        /// <summary>   A flag indicating if the static bodies have changed in this frame. </summary>
        public NativeReference<int> HaveStaticBodiesChanged;

        /// <summary>   Group in which the dynamic bodies belong to. </summary>
        public EntityQuery DynamicEntityGroup;
        /// <summary>   Group in which the static bodies belong to </summary>
        public EntityQuery StaticEntityGroup;
#if !UNITY_PHYSICS_DISABLE_JOINTS
        /// <summary>   Group in which the joints belong to </summary>
        public EntityQuery JointEntityGroup;
#endif

        /// <summary>
        /// The component handles. Stores the information about ECS component handles needed for
        /// generating a <see cref="PhysicsWorld"/>
        /// </summary>
        public PhysicsWorldComponentHandles ComponentHandles;

        /// <summary>
        /// The physics world component handles. Stores the information about ECS component handles
        /// needed for generating a <see cref="PhysicsWorld"/>
        /// </summary>
        public struct PhysicsWorldComponentHandles
        {
            /// <summary>   Constructor. </summary>
            ///
            /// <param name="systemState">  [in,out] State of the system. </param>
            public PhysicsWorldComponentHandles(ref SystemState systemState)
            {
                EntityType = systemState.GetEntityTypeHandle();
                LocalToWorldType = systemState.GetComponentTypeHandle<LocalToWorld>(true);
                ParentType = systemState.GetComponentTypeHandle<Parent>(true);

                LocalTransformType = systemState.GetComponentTypeHandle<LocalTransform>(true);
                PhysicsColliderType = systemState.GetComponentTypeHandle<PhysicsCollider>(true);
                PhysicsVelocityType = systemState.GetComponentTypeHandle<PhysicsVelocity>(true);
                PhysicsMassType = systemState.GetComponentTypeHandle<PhysicsMass>(true);
                PhysicsMassOverrideType = systemState.GetComponentTypeHandle<PhysicsMassOverride>(true);
                PhysicsDampingType = systemState.GetComponentTypeHandle<PhysicsDamping>(true);
                PhysicsGravityFactorType = systemState.GetComponentTypeHandle<PhysicsGravityFactor>(true);
                PhysicsCustomTagsType = systemState.GetComponentTypeHandle<PhysicsCustomTags>(true);
                PhysicsConstrainedBodyPairType = systemState.GetComponentTypeHandle<PhysicsConstrainedBodyPair>(true);
                DisablePhysicsColliderType = systemState.GetComponentTypeHandle<DisablePhysicsCollider>(true);
#if !UNITY_PHYSICS_DISABLE_JOINTS
                PhysicsJointType = systemState.GetComponentTypeHandle<PhysicsJoint>(true);
#endif
                SimulateType = systemState.GetComponentTypeHandle<Simulate>(true);
            }

            /// <summary>
            /// Updates the <see cref="PhysicsWorldComponentHandles"/>. Call this in OnUpdate() methods of
            /// the systems in which you want to store PhysicsWorldData in.
            /// </summary>
            ///
            /// <param name="systemState">  [in,out] State of the system. </param>
            public void Update(ref SystemState systemState)
            {
                EntityType.Update(ref systemState);
                LocalToWorldType.Update(ref systemState);
                ParentType.Update(ref systemState);

                LocalTransformType.Update(ref systemState);

                PhysicsColliderType.Update(ref systemState);
                PhysicsVelocityType.Update(ref systemState);
                PhysicsMassType.Update(ref systemState);
                PhysicsMassOverrideType.Update(ref systemState);
                PhysicsDampingType.Update(ref systemState);
                PhysicsGravityFactorType.Update(ref systemState);
                PhysicsCustomTagsType.Update(ref systemState);
                PhysicsConstrainedBodyPairType.Update(ref systemState);
                DisablePhysicsColliderType.Update(ref systemState);
#if !UNITY_PHYSICS_DISABLE_JOINTS
                PhysicsJointType.Update(ref systemState);
#endif
                SimulateType.Update(ref systemState);
            }

            internal EntityTypeHandle EntityType;
            internal ComponentTypeHandle<LocalToWorld> LocalToWorldType;
            internal ComponentTypeHandle<Parent> ParentType;

            internal ComponentTypeHandle<LocalTransform> LocalTransformType;

            internal ComponentTypeHandle<PhysicsCollider> PhysicsColliderType;
            internal ComponentTypeHandle<PhysicsVelocity> PhysicsVelocityType;
            internal ComponentTypeHandle<PhysicsMass> PhysicsMassType;
            internal ComponentTypeHandle<PhysicsMassOverride> PhysicsMassOverrideType;
            internal ComponentTypeHandle<PhysicsDamping> PhysicsDampingType;
            internal ComponentTypeHandle<PhysicsGravityFactor> PhysicsGravityFactorType;
            internal ComponentTypeHandle<PhysicsCustomTags> PhysicsCustomTagsType;
            internal ComponentTypeHandle<PhysicsConstrainedBodyPair> PhysicsConstrainedBodyPairType;
            internal ComponentTypeHandle<DisablePhysicsCollider> DisablePhysicsColliderType;
#if !UNITY_PHYSICS_DISABLE_JOINTS
            internal ComponentTypeHandle<PhysicsJoint> PhysicsJointType;
#endif
            internal ComponentTypeHandle<Simulate> SimulateType;
        }

        /// <summary>   Constructor. </summary>
        ///
        /// <param name="state">      [in,out] The <see cref="SystemState"/> of the system in which you
        /// want to use <see cref="PhysicsWorldData"/>. </param>
        /// <param name="worldIndex">   Zero-based index of the world. </param>
        public PhysicsWorldData(ref SystemState state, in PhysicsWorldIndex worldIndex)
        {
            PhysicsWorld = new PhysicsWorld(0, 0, 0);
            HaveStaticBodiesChanged = new NativeReference<int>(Allocator.Persistent);

            EntityQueryBuilder queryBuilder = new EntityQueryBuilder(Allocator.Temp)

                .WithAll<PhysicsVelocity, LocalTransform, PhysicsWorldIndex>();

            DynamicEntityGroup = state.GetEntityQuery(queryBuilder);
            DynamicEntityGroup.SetSharedComponentFilter(worldIndex);
            queryBuilder.Dispose();

            queryBuilder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PhysicsCollider, PhysicsWorldIndex>()

                .WithAny<LocalToWorld, LocalTransform>()

                .WithNone<PhysicsVelocity>();
            StaticEntityGroup = state.GetEntityQuery(queryBuilder);
            StaticEntityGroup.SetSharedComponentFilter(worldIndex);
            queryBuilder.Dispose();

#if !UNITY_PHYSICS_DISABLE_JOINTS
            queryBuilder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PhysicsConstrainedBodyPair, PhysicsJoint, PhysicsWorldIndex>();
            JointEntityGroup = state.GetEntityQuery(queryBuilder);
            JointEntityGroup.SetSharedComponentFilter(worldIndex);
            queryBuilder.Dispose();
#endif

            ComponentHandles = new PhysicsWorldComponentHandles(ref state);
        }

        /// <summary>
        /// Calls the <see cref="PhysicsWorldComponentHandles.Update(ref SystemState)"></see> of the
        /// handles stored in this object. /&gt;
        /// </summary>
        ///
        /// <param name="state">    [in,out] The state. </param>
        public void Update(ref SystemState state)
        {
            ComponentHandles.Update(ref state);
        }

        /// <summary>
        /// Free stored memory.
        /// </summary>
        public void Dispose()
        {
            PhysicsWorld.Dispose();
            if (HaveStaticBodiesChanged.IsCreated)
            {
                HaveStaticBodiesChanged.Dispose();
            }
        }
    }
}
